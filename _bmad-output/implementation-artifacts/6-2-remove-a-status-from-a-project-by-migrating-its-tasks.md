---
baseline_commit: e9c39fbc3847fc5a6919e4f65485e2ee20c63bb3
---

# Story 6.2: Remove a Status from a Project by migrating its Tasks

Status: done

Epic: 6 — Shape Statuses per Project and retire work safely (second of six stories)
Story key: `6-2-remove-a-status-from-a-project-by-migrating-its-tasks`

**Requirements owned.** From the Story Coverage Index (`epics.md:381`): **FR-26**, **FR-25** (the remove half, and "a removed Status can be added back at any time"), **AR-24**. Also carried here:

- **AD-17** (`ARCHITECTURE-SPINE.md:193-197`): removing a Status and remapping every occupying Task is one transaction with no partial application. No endpoint accepts a removal without the mapping it requires. An invariant test asserts no Task ever holds a Status absent from its Project's effective set.
- **AD-16** (`ARCHITECTURE-SPINE.md:186-191`): the removal is an operation of the delta keyed on Status id, never a materialised list; the effective set stays derived on read.
- **S-1** (`test-design-qa.md:318`, **P0**): atomicity under a forced mid-operation failure. 6.2 owns the "Status removal + remap" case of the six.
- **S-2** (`test-design-qa.md:319`, **P0**): no Task holds a Status absent from its Project's set. 6.2 is the first story in which a Task's **current** Status can leave its Project's set, so this is where the invariant is first really at risk.
- **FR-16** (`prd.md:346`): `Capability.ConfigureProjectStatusDelta` (Owner/Admin) covers removal and add-back.
- **NFR-1 / FR-15 / AD-2 / AD-3** (I-1..I-4, A-4, A-5, P0): new routes carry `{spaceId}` and get isolation rows on both surfaces.
- **UX** (`EXPERIENCE.md:215`, `:257-260`, `:345`, `:364`; DESIGN.md `picker`, `dialog`; mockup `project-settings.html` §1 and §4): the × per row, the destination picker with **no default selection**, the in-flight state (named scope, focus to the progress region, no percentage) and the refused state ("Nothing was moved.").

**Ledger entries owned (`deferred-work.md`). Close each one, or re-own it with a reason:**

| Line | Entry | Recommended disposition |
|---|---|---|
| `:1020` | Task Status validation runs before the `ProjectTasks` lock (medium, owner 6.2) | **CLOSE** (AC8, Task 4, Q6) |
| `:1022` | A `Place` whose anchor leaves the set is ignored (low, owner 6.2) | **CLOSE** (AC10, Q3): removal re-anchors every dependent `Place` |
| `:1032` | `Sequence` has no unique database backstop (low, owner 6.2) | **CLOSE** (Task 2, Q9) |
| `:655` | A Status-sort List cursor whose Status left the set ends the walk (medium once reachable, owner 6.2) | **CLOSE** (AC15, Q10) |
| `:676` | A selected pager Status that leaves the set stays selected; `PagerTabs` rebuilt every render (low, owner 6.2) | **CLOSE the stale selection** (AC15); the allocation half per Q10 |
| `:114` | The first default Status name may not appear in any comment (S1135). Owner Epic 6 | **Keep open**, a standing trap for 6.2-6.6 (see Traps) |
| `:1030`, `:1031` | Name comparison vs collation; look-alike names (owner epic 6) | Leave open; not touched by removal. Note in the ledger that 6.2 left them. |
| `:1038`, `:1039` | 6.1 migration not safe for the old revision; `Down()` lossy (owner epic 6) | Leave open; a 6.2 migration (Q9) must not make either worse. |
| `:1040` | The AD-16 note claims an ER edge the diagram lacks (owner epic 6) | **CLOSE** with the AD-16 note this story adds (Task 7) |
| `:1041` | BoardView's comment still calls `ViewToggle` parameterless (owner 6.1, "sweep with the next BoardView change") | **CLOSE** if Task 6 touches `BoardView`; otherwise leave |

## What already exists — do not rebuild it

| Concern | Where it lives today | What 6.2 does with it |
|---|---|---|
| Delta operation entity | `Yello.Domain/Statuses/StatusDeltaOperation.cs`: `Id`, `SpaceId`, `ProjectId`, `StatusId`, `Kind` (`Add`, `Rename`, `Place`), `Name`, `AfterStatusId`, `Sequence`, `CreatedAt`. `Kind` is stored as a string (`varchar(16)`, `HasConversion<string>`) | **Add `Remove` to `StatusDeltaKind`.** No column change is needed for it. Update the class remarks ("bounded by Statuses × three kinds" becomes four). |
| The one derivation | `Yello.Domain/Statuses/EffectiveStatusSet.cs`: `Of(defaults, delta)` applies ops in `Sequence` order; `Check(after, namedStatusId)` refuses `SetEmpty` and `NameTaken`; `Placements(current, placed, statusId, afterStatusId)` re-anchors every already-placed Status left to right; `Contains` | **Extend `Apply` with `Remove`** (drop the Status from the list if present). Reuse the `Placements` idea for the re-anchoring (AC10). `Check`'s `SetEmpty` becomes reachable through HTTP (AC6). Do not add a second derivation. |
| Delta as read | `ProjectStatusDelta(OwnedDefinitions, Operations)`, `With(added, operations)`, `NextSequence` | `With` replaces by (Status, Kind); a removal also **deletes** ops and an owned definition, so `With` needs a "minus" form (or a second method) to describe the delta after the removal for `Check`. |
| Draft and writer | `ProjectStatusDeltaDraft` (plans `Add`, `Rename`, `Place` under the lock); `ProjectStatusDeltaWriter.WriteAsync(spaceId, projectId, plan, ct)`: `store.LockAsync` → read defaults + delta → plan → `Check` → `store.WriteAsync` → re-read and answer `ProjectStatusSet` | **The removal goes through the same writer** (lock, read under the lock, plan, check, write, answer the set). It needs extra store work (Task migration, op deletes, owned-definition delete), so extend `IProjectStatusStore` rather than bypass the writer. |
| Store | `Yello.Infrastructure/Persistence/ProjectStatusStore.cs`: `LockAsync` (plain read, `ApplicationLocks.TakeProjectTasksAsync`, re-read), `WriteAsync` (delete same Status+Kind, insert, rename owned definition, detach on 2601/2627) | Add the removal's statements (Task 2). Same lock; nothing new. |
| Lock | `ApplicationLocks.ProjectTasks(spaceId, projectId)` (`ApplicationLocks.cs:64`, taker `:196`): exclusive, transaction-owned `sp_getapplock`, re-entrant within the request's transaction. Taken by: the Tasks-per-Project bound count (`BoundCounter.cs:132-140`, so **Task creation already reads its Status under the lock**), `TaskStore.ApplyAsync` (Status edits, `:169-187`), `TaskStore.MoveAsync` (`:408`), Task deletion, Project deletion, every delta write | The removal takes it once. Edit and Move must **validate under it** (AC8). |
| Task writers | `EditTaskAttributesHandler.cs:57-65` and `MoveTaskHandler.cs:47-52` read `EffectiveOfAsync(task.ProjectId)` **before** the store takes the lock; `CreateTaskHandler.cs:49-57` reads it after the pipeline's bound count took it | Edit and Move: move the check under the lock (Q6). Create: prove it with a race case; change nothing unless the case fails. |
| Task position keys | `TaskConfiguration.cs:95-96`: unique `(SpaceId, ProjectId, StatusId, PositionKey)`, `PositionKey` `Latin1_General_100_BIN2`, max 128; `PositionKey.After(last)` (`Yello.Domain/Tasks/PositionKey.cs:81`); "last key" read under the lock (`TaskStore.cs:528-536`); `OPENJSON` already used for a set-based statement (`TaskStore.cs:249-252`) | Migrated Tasks get `After(last)`, `After(that)`, ... in the source column's order (AC9). Never renumber a destination sibling (AD-15). |
| Project deletion | `ProjectStore.DeleteAsync` (`:98-157`): ordered statements under `ProjectsPerSpace`, the row `UPDLOCK`, `ProjectTasks`: Tasks → delta ops → owned definitions → Project | Unchanged. A `Remove` op is a delta row and goes with the others. Prove it with a populated case (Project with a removed default). |
| Space deletion | One statement; NO ACTION keys checked at statement end | Unchanged; covered by 6.1's populated case. Add a removed default to its seed. |
| Routes | `Yello.Contracts/Projects/ProjectStatusRoutes.cs`: `Collection` (GET, POST add), `Item` (PATCH rename), `Place` (POST action). `ProjectStatusResponse(Id, Name, Source, DefaultName, TaskCount)`; sources `space-default` / `added-here` | Add the remove and add-back routes (Q1, Q2). Every answer stays the whole set as `ProjectStatusResponse` rows. **Never change `ProjectStatusResponse` or `StatusResponse` in a breaking way** (C-1, C-2). |
| Endpoints | `Yello.Host/Endpoints/ProjectStatusEndpoints.cs` (`JsonRequestBody.ReadAsync`, `ProjectStatusAnswers.Refusal(outcome)`, `BoundaryProblems.NotAvailable()`); `ProjectStatusAnswers.cs` maps `ProjectStatusWriteOutcome` → problem | Add two endpoints and the new outcomes. Expect S1200/S1541 to force a split. |
| Problem types | `ProblemTypes.cs`: `invalid-status-name`, `status-name-taken`, `status-set-empty` (`:283`, unreachable until now), `invalid-status-position`, `invalid-task-status`, `invalid-list-cursor` (`:367`) | Add `status-destination-required` (409) and `invalid-status-destination` (400) (Q4). |
| Space default read | `GET /api/v1/spaces/{spaceId}/statuses` → Space defaults only (`StatusResponse(Id, Name, Position)`) | The editor derives "removed here" = Space defaults whose id is not in the Project's set (Q2). No new read. |
| Client editor | `Yello.Client/Components/StatusDeltaEditor.razor` (413 lines: load, purge, `WriteAsync` with key reuse, `LiveAnnouncement`, `InlineErrorRegion`, a write's `404` → `OnRefused` → page refusal) and `StatusDeltaRow.razor` (189 lines; its remarks say "No remove control") | Add the remove control, the mapping dialog, the progress region and the add-back list. Split into new components; both files are near their analyzer caps. |
| Client pieces to reuse | `MovePicker.razor`, `PickerOption.razor` (the `{components.picker}` treatment), `OwnershipOfferDialog.razor` (a dialog with focus handling), `LiveAnnouncement`, `InlineErrorRegion`, `RefusalCopy`, `ClientCopy.resx`, `StatusCapabilities.PermitsConfigureProjectStatusDelta` (gated) | Reuse. No new client predicate is needed: removal is the same capability. |
| Board/pager/picker | `BoardView.razor(.cs)`, `BoardView.Pager.cs` (`SelectedColumn`, `PagerTabs`), Task detail's Status picker — all drawn from the server's answer | They follow the new set on the next read with no change, except the pager selection (AC15). |
| List reader | `ListReader.ByStatusAsync` (`ListReader.cs:112-156`): a cursor whose Status is not in `statusOrder` returns an empty page with no cursor | AC15. |
| S-2 helper | `WireDatabase.TasksOutsideTheirProjectsSetSql` (`tests/Yello.Tests.Shared/WireDatabase.cs:805-817`): "a Task is outside when its Status is another Project's definition, or none" — **its remarks say "with no operation that removes a Status"** | **It must learn `Remove`**: a Task whose Status its own Project's delta removes is outside. Otherwise S-2 passes vacuously (plant 1). |
| S-1 pattern | `ActiveSpaceEndpointTests.DeleteSpace.cs:268-305`, `:774-800`: a test-only `AFTER` trigger that `THROW`s for one id, dropped in a `finally`, anchored on the same request succeeding once dropped | Copy it for the removal (AC3). |
| Isolation rows | `tests/Yello.Tests.Isolation/IsolationCases.ProjectStatuses.cs` (nine rows, incl. `ISO-*-FOREIGN-STATUS-IN-OWN-PROJECT`), `IsolationCases.cs:773-790` (`ISO-MOVE-FOREIGN-STATUS-IN-OWN-SPACE`: a body-borne foreign Status → `400`), the `ISO-TOKEN-OWN-OTHER-SPACE-PROJECT-STATUS*` rows `Every_Space_scoped_route_a_Token_reaches_has_a_row_...` requires | Extend with the new routes. |
| Route gates | ARCH-502 / ARCH-503 (`tests/Yello.Tests.Architecture/RefusalBoundaryGateTests.Delete.cs`), widened by 6.1 by exactly four Project Status routes; `RefusalBoundaryGateTests.ProjectStatuses.cs` (the no-mode gate, the Space read's shape) | Widen ARCH-503 by exactly the new routes. Add the "no removal without its mapping" gate (AC4). |
| Fake API | `tests/Yello.Tests.Client/FakeYelloApi.ProjectStatuses.cs` (serves the Project's set and the Board's columns from it) | Add remove (with the 409 and both 400s) and add-back. |

**What is genuinely new in 6.2:**

1. A `Remove` delta kind and its effect on the derivation.
2. One removal operation that, under the Project's lock and in one transaction, migrates every occupying Task, re-anchors dependent places, records the removal (or deletes an owned definition), and answers the new set.
3. Add-back of a removed Space default, keeping its id.
4. Task edit and move validate the Status under the Project's lock.
5. The remove control, the mapping dialog with a no-default destination picker, the in-flight progress region and the refused state.
6. The S-2 helper understands removal; S-1's removal case.

**Explicitly not in this story:**

- The Space defaults editor and any Space-level removal or cascade (6.3, 6.4). The Space read stays defaults-only and unchanged.
- Moving Tasks to another Project (6.5, 6.6) and the `bulk-move-bar` component (UX-DR17, 6.6). 6.2's progress region is the Project-settings "Status migration in flight" state (`EXPERIENCE.md:259`), not the bulk-move bar, and it is **not** accent-bordered.
- Removing several Statuses in one operation. One Status per request (Q5).
- A bound on Statuses per Project (ledger `:1026`, Lee/PM).
- No new NuGet package, no new project. No commit, push, branch, Azure login or deploy. The orchestrator commits.

## Story

As an Owner or Admin,
I want removing a Status to move the Tasks sitting in it rather than stranding them,
so that a Task can never hold a Status its Project does not expose.

## Acceptance Criteria

1. **An occupied Status needs a destination in the same operation** (FR-26, AD-17). Given a Status of the Project's effective set that at least one of the Project's Tasks holds, a removal request without a destination is refused `409 status-destination-required` and writes nothing. With a valid destination, every occupying Task moves to it and the Status leaves the set, in one request. "Occupied" is decided **under the Project's lock**, so a Task created into or moved into the Status after the client read its count is still migrated, or the request is refused; it is never stranded.

2. **The destination is validated against the set in force after the removal** (FR-26). The destination must be in the Project's effective set minus the removed Status, read under the lock. The removed Status itself, a random id, another Space's Status, a sibling Project's added Status and a Space default this Project has already removed are all refused with the **same** `400 invalid-status-destination` body, byte for byte, and nothing is written.

3. **One transaction, no partial application** (AD-17, AR-24, S-1 P0). The Task migration, the delta change and any owned-definition delete commit together or not at all. Proved by a failure forced after the migration statement and before commit (a test-only trigger, as `DeleteSpace.cs:268-305`): the answer is `5xx`; every Task still holds the removed Status at its old key; the Status is still in the set; S-2 holds. Anchored on the same request succeeding once the trigger is dropped.

4. **No endpoint accepts a removal without the mapping it requires** (AR-24). The only route that removes a Status from a Project is the remove route. Its request carries the destination. No other write (add, rename, place, the Task routes) can remove a Status or make one unreachable. An architecture gate asserts both: the remove request record has a destination member, and no other Project Status route's request can carry a removal.

5. **An unoccupied Status is removed directly** (FR-26). With no occupying Task, no destination is required and the removal succeeds. A destination supplied anyway is still validated (AC2), so a request means one thing whatever the occupancy.

6. **Never empty** (FR-25). Removing the only Status of the set is refused `409 status-set-empty`, before any destination check, and writes nothing. 6.1's domain guard (`EffectiveStatusSet.Check`) is now reachable over HTTP; prove it there.

7. **A removed Status can be added back at any time** (FR-25):
   - A removed **Space default** is added back by id through the add-back route (Q2). It keeps its id, so a later Space-level rename (6.3) still reaches it. It returns with its Space name at its Space-order slot (Q3). If its name now equals another name in the set, the add-back is refused `409 status-name-taken`.
   - A removed **added-here** Status is gone (its definition was deleted, Q1). Adding the same name again through the existing add route succeeds and makes a new Status with a new id. The owned-name index no longer holds the old name.
   - Asserted: remove, add back, remove again, add back again, each answering the set; S-2 after each.

8. **No Task ever holds a Status absent from its Project's set — before, during, or after** (AD-17, S-2 P0; ledger `:1020`). The S-2 statement understands `Remove`, and it is asserted after every case this story adds. Task edit and move check the Status **under the Project's lock**, so:
   - an edit or a move to Status S that queues behind S's removal is refused `400 invalid-task-status` (unchanged body);
   - a removal that queues behind an edit or a move into S migrates that Task;
   - a creation queued behind the removal of the Project's first Status lands in the new first Status.

   Each race is proved deterministically by holding one side and waiting on `LCK_M_%` (`TESTING-CONVENTIONS.md:102-180`), never by timing.

9. **Migrated Tasks keep everything but their Status and key** (AD-15). Each migrated Task lands at the **end** of the destination column, in its source-column order. Its id, title, Assignee, Labels, due date and creation time are unchanged. No Task already in the destination column changes its key. The keys stay unique under `(SpaceId, ProjectId, StatusId, PositionKey)`.

10. **The rest of the order does not move** (ledger `:1022`, Q3). After a removal, the remaining Statuses appear in the same relative order as before it. Every `Place` operation anchored on the removed Status is re-anchored, so no Status jumps back to its Space-order slot because its anchor left. The removed Status's own `Rename` and `Place` operations are deleted. Asserted with a Project whose places anchor on the Status being removed.

11. **Role** (FR-16, UX-DR27, C-6). Both new routes declare `ConfigureProjectStatusDelta`. Member and Viewer get `403` with the pipeline's refusal record, on Session and Token. In the client, the remove control, the dialog and the add-back controls are **absent from the DOM** for Member and Viewer (not disabled).

12. **Isolation** (NFR-1, FR-15, AD-2, AD-3; I-1..I-4, A-4). Both routes carry `{spaceId}`. A foreign Space, a foreign Project in the caller's own Space path, and another Space's or a sibling Project's Status in the path each answer the boundary's byte-identical `404 not-available`, write nothing and record what the boundary rules say. A foreign destination in the body is AC2's `400`, identical to a random id. Rows go in `IsolationCases` on both surfaces, live before review (GATE-003).

13. **Idempotent, and the answer is the truth** (AD-18). Both writes go through the pipeline's `Idempotency-Key`. A replay returns the original response and migrates nothing again. Each write answers `200` with the Project's whole set (`ProjectStatusResponse` rows, Task counts included), so the editor re-renders from the server's answer.

14. **Client** (UX-DR15, UX-DR16's no-default rule, UX-DR27, UX-DR31, UX-DR42, NFR-9; `EXPERIENCE.md:257-260`):
    - Each row gets a "Remove" control (Owner/Admin), named for its Status (`aria-describedby` the name, as the move buttons do).
    - An unoccupied Status is removed directly, then announced. If the server answers `409 status-destination-required` (the count was stale), the client re-reads and opens the dialog.
    - An occupied Status opens a dialog that states the scope ("Removing "{name}" moves {n} Tasks.") and holds a destination picker over the **post-removal** set. The picker **has no default selection**: it starts on a placeholder ("Choose…") that is not a Status. Committing with no choice is refused locally in the field's message (Q7). Cancel closes it and returns focus to the row's Remove control.
    - **In flight:** the dialog's commit is replaced by a progress region with `role="status"`, naming the scope ("Moving {n} Tasks to "{destination}"."). Focus moves to it (`tabindex="-1"`). **No percentage, no progress bar.** Blocking never destroys the focused node.
    - **Done:** a polite announcement ("Removed "{name}". {n} Tasks moved to "{destination}"."), focus to the destination's row (or to the list heading for a direct removal).
    - **Refused:** "Nothing was moved." plus the reason, in the existing inline error region with localised copy (`RefusalCopy`, `ClientCopy.resx`). A write's `404` for a Status that has left the set re-reads the set before deciding it is a page-level refusal (Q8).
    - Removed Space defaults are listed under the add form with an "Add back" control each (Q2). Absent for Member and Viewer.
    - Logical properties, usable at 320px, copy not sized to English. Purged on Space switch, sign-out and Account switch.

15. **Readers after a Status leaves the set** (ledger `:655`, `:676`; Q10).
    - The List View's Status-sort cursor naming a Status no longer in the order answers `400 invalid-list-cursor` (existing type and client handling) instead of an empty page that silently ends the walk.
    - The Board pager drops a selected Status that is no longer a column (clears `_selectedStatusId`), so it is never re-selected without the person choosing it.
    - A Board column page for a removed Status answers `404` (unchanged, PG-07).

16. **Scale** (NFR-8, AD-17). A removal migrating 5,000 Tasks (the Tasks-per-Project bound) commits as one transaction and leaves S-2 holding. This is a tiered case (`[Trait("Tier", "Nightly")]`, `Explicit = true`), not a PR-stage case (Q11).

## Tasks / Subtasks

- [x] **Task 1 — Domain: `Remove` and its derivation** (AC1, AC2, AC5, AC6, AC7, AC10)
  - [x] Add `StatusDeltaKind.Remove`. `EffectiveStatusSet.Apply`: a `Remove` drops the Status from the list if present. Keep the class remarks true: delete the sentence "a place whose anchor is not in the set is ignored ... needs nothing from whoever removed the anchor" or rewrite it (the anchor's removal now re-anchors; the ignore rule remains as a deterministic fallback).
  - [x] A pure planner for the re-anchoring (AC10): given the set before, the placed Statuses and the removed id, return the `Place` operations that replay to "the set before, minus the removed Status", sequenced left to right. Reuse or generalise `Placements` (do not write a third ordering algorithm).
  - [x] A pure destination check: the destination is in `Of(defaults, delta-after-removal)`. Reuse `Contains`.
  - [x] `ProjectStatusDelta`: a way to describe the delta after a removal (ops deleted, an owned definition dropped, new ops added) so `Check` sees the real post-removal set.
  - [x] Unit tests (`tests/Yello.Tests.Slices/Statuses/EffectiveStatusSetTests.cs`): remove a default; remove an added Status; remove one whose id anchors others' places (order of the rest unchanged); remove then restore a default (Space slot, Space name); remove the last (`SetEmpty`); a destination equal to the removed Status is not in the after-set; two Projects, one removes a default, the other still holds it.
- [x] **Task 2 — Infrastructure: the removal's statements** (AC1, AC3, AC9, AC10; ledger `:1032`)
  - [x] Extend `IProjectStatusStore` (Domain port) with what the removal needs, all in the request's unit of work, after `LockAsync`:
    - `OccupantsAsync(statusId)` or a count, read under the lock;
    - `MigrateAsync(fromStatusId, toStatusId)`: read the destination column's last key, then give each occupying Task (ordered by its own `PositionKey` in BIN2, then `Id`) `PositionKey.After(previous)`, in **one** set-based `UPDATE` (for example `UPDATE t SET StatusId = @to, PositionKey = j.k FROM dbo.[Task] t JOIN OPENJSON(@map) ...`, as `TaskStore.cs:249-252` already uses `OPENJSON`). Never 5,000 statements. Parameters only; no `Where SpaceId` (the policy narrows it), raw SQL only in Infrastructure.
    - the delta change: delete the removed Status's `Rename` and `Place` ops; delete-and-reinsert the re-anchored `Place` ops; then either insert a `Remove` op (a default) or delete the owned definition's `Add` op and the definition itself (an added Status; Q1).
  - [x] Statement order matters (all NO ACTION): Tasks off the Status first; then ops that reference it (`StatusId` or `AfterStatusId`); then the owned definition. Write the reason in the store's remarks, as `ProjectStore.DeleteAsync` does.
  - [x] Add-back: delete the default's `Remove` op. Nothing else.
  - [x] Ledger `:1032` (Q9): a unique index `(SpaceId, ProjectId, Sequence)` on `StatusDeltaOperation`. A new migration after `20261121155445_ProjectStatusDelta`, hand-checked (`TESTING-CONVENTIONS.md:599`). The replace pattern already deletes before it inserts and every write takes `NextSequence` under the lock, so no existing write collides; prove it with the 6.1 write cases. `dotnet tool restore` first, then `dotnet ef` 10.0.11. If the dev decides against the index, re-own the entry with the reason.
- [x] **Task 3 — Application: the remove and add-back slices** (AC1-AC7, AC13)
  - [x] `Yello.Application/Statuses/RemoveProjectStatus/RemoveProjectStatusHandler.cs` and `.../RestoreProjectStatus/RestoreProjectStatusHandler.cs` (names are the dev's), both through `ProjectStatusDeltaWriter` (lock → read → plan → check → write → answer the set).
  - [x] Order of decisions under the lock: Status not in the set → `NotFound`; set of one → `SetEmpty`; destination supplied and not in the after-set → `DestinationInvalid`; occupied and no destination → `DestinationRequired`; otherwise write.
  - [x] Add-back: the id must be a Space default this Project's delta removes; otherwise `NotFound` (Q2). Name collision → `NameTaken` (via `Check`).
  - [x] New `ProjectStatusWriteOutcome` members: `DestinationRequired`, `DestinationInvalid`.
  - [x] Handler tests with fakes (no database), in `tests/Yello.Tests.Slices/Statuses/`.
- [x] **Task 4 — Task writers validate under the lock** (AC8; ledger `:1020`)
  - [x] Q6 (a): add `ITaskStore.LockProjectAsync(projectId, ct)` (the same `ProjectTasks` lock; re-entrant). `EditTaskAttributesHandler` (only when `edit.StatusId` is set) and `MoveTaskHandler` take it **before** reading `EffectiveOfAsync(task.ProjectId)`. The store's own later take is then a no-op re-entry; keep it, because the store must stay safe on its own.
  - [x] After the lock, re-read the Task's `ProjectId` (6.5 will reparent Tasks; a stale `ProjectId` would validate against the wrong set). Not found under the lock → the existing `NotFound`.
  - [x] `CreateTaskHandler`: already under the lock via the bound count. Add a remark naming that dependency, and the race case. Do not add a second lock.
  - [x] Update the remarks of the three handlers and `TaskStore` (lock order: Project lock, then Task row, then Labels, unchanged).
- [x] **Task 5 — Contracts and Host** (AC2, AC4, AC11-AC13)
  - [x] Q1/Q2 routes in `ProjectStatusRoutes`: `Remove` = `POST …/projects/{projectId}/statuses/{statusId}/remove` with `RemoveProjectStatusRequest(Guid? DestinationStatusId)`; `Restore` = `POST …/projects/{projectId}/statuses/{statusId}/restore` with no body. Path helpers `RemoveOf`, `RestoreOf`.
  - [x] `ProblemTypes.StatusDestinationRequired` (`urn:yello:problem:status-destination-required`, 409) and `ProblemTypes.InvalidStatusDestination` (`urn:yello:problem:invalid-status-destination`, 400). Map both in `ProjectStatusAnswers`. An unreadable remove body is `400 invalid-status-destination`.
  - [x] Endpoints in `ProjectStatusEndpoints` (or a split sibling file if S1200 forces it), `RequiresCapability(ConfigureProjectStatusDelta)`, answering `200` with the set. Register them in `Program.cs`.
  - [x] Widen ARCH-503 by exactly the two routes and update its remarks. Add the AC4 gate. Add `ConfigureProjectStatusDelta`'s remove to the role-matrix theory if the theory needs a representative per route (`ActiveSpaceEndpointTests.RoleMatrix.cs`).
  - [x] If a contract snapshot of served types exists (AD-19, C-1), add the new request record.
- [x] **Task 6 — Client** (AC7, AC11, AC14, AC15)
  - [x] `StatusDeltaRow`: a "Remove" button (Owner/Admin), raised to the editor. Update its remarks ("No remove control" is no longer true).
  - [x] A new `StatusRemoveDialog.razor` (name per the dev): scope line, the destination picker with a placeholder and no default (reuse the `{components.picker}` treatment of `MovePicker`/`PickerOption`, or a native `<select>` whose first option is the empty placeholder), commit (`button-primary`, "Remove and move {n} Tasks"), Cancel (`button-secondary`). It is not a `destructive-confirm`: nothing is lost, and danger is reserved for the irreversible (DESIGN.md `:322`, `:501`).
  - [x] The progress region and its focus rule; the done announcement and focus; the refused state ("Nothing was moved." + reason).
  - [x] Removed Space defaults: read `GET /api/v1/spaces/{spaceId}/statuses` alongside the Project's set and list the defaults absent from it, each with "Add back". Purge both reads with the Space.
  - [x] A write's `404`: re-read the set; if the read succeeds, show "That Status is no longer in this Project." inline and render the new set; only a failing read raises `OnRefused` (Q8).
  - [x] Pager: `BoardView.Pager.cs` `SelectedColumn` clears a selection that is not a column. If `BoardView` is touched, also fix the stale `ViewToggle` comment (ledger `:1041`).
  - [x] Copy in `ClientCopy.resx` (sentence case); map the two new problem types in `RefusalCopy`.
  - [x] `FakeYelloApi.ProjectStatuses.cs`: remove (incl. the `409` and both `400`s) and add-back, and the Board's columns follow.
- [x] **Task 7 — List cursor, ledger and docs** (AC15; all ledger rows above)
  - [x] `ListReader.ByStatusAsync` / `ViewListHandler`: a Status-sort cursor whose Status is not in the order answers `400 invalid-list-cursor` (Q10). Check `ListEndpoints.cs:73`'s mapping.
  - [x] Write the disposition of every owned ledger entry in `deferred-work.md` (CLOSED / UPDATED / RE-OWNED, with the reason). Do not leave one blank.
  - [x] AD-16: a dated, additive note that the delta gains `Remove` for a default and that removing an added Status deletes its definition (Q1), and add the `PROJECT ||--o{ STATUS_DEFINITION : adds` edge to the ER that 6.1's note already claims (closes `:1040`). Additive only; no rule changes.
- [x] **Task 8 — Tests** (see Testing requirements)
  - [x] Teach `WireDatabase.TasksOutsideTheirProjectsSetSql` about `Remove`, rewrite its remarks, and assert it after every case added here.
  - [x] Slices partials, handler tests, the S-1 case, the race cases, isolation rows, gates, browser class.

### Review Findings

Code review 2026-11-21 (Blind Hunter, Edge Case Hunter, Acceptance Auditor; range `0c017f7..HEAD`): 41 raw findings, 33 after de-duplication: 11 patch, 10 deferred as low, 0 decisions, 12 dismissed. The dismissed findings are decisions the story records (Q6's extra read, Q10's cursor `400`, the bound count's lock for creation, an unreadable remove body being `400`), the pager case already in the ledger (owner 6.3), the plants (run in this phase), a refusal's focus (the inline error region takes it), an unreachable longest key, the new index's data check, and the Board's cold load (which starts with no selection).

- [x] [Review][Patch] In flight, the pressed commit is removed in the same render that adds the progress region, so the focused node is destroyed before focus moves (AC14 "Blocking never destroys the focused node"); a second press in that window re-enters the removal [Yello.Client/Components/StatusRemoveDialog.razor:36]
- [x] [Review][Patch] The dialog's focus trap sends both sentinels to the heading, so Shift+Tab from the heading never wraps to the last control [Yello.Client/Components/StatusRemoveDialog.razor:25]
- [x] [Review][Patch] "Add back" removes the pressed button from the DOM and asks for no focus target, so focus falls to the body [Yello.Client/Components/StatusDeltaEditor.Removal.cs:145]
- [x] [Review][Patch] Removing the only Status when Tasks hold it opens a dialog with no destination to choose and no reason; it should go to the server, which answers `409 status-set-empty` [Yello.Client/Components/StatusDeltaEditor.Removal.cs:90]
- [x] [Review][Patch] The add-back round trip asserts S-2 only twice, not after each removal and add-back (AC7) [tests/Yello.Tests.Slices/Projects/ProjectEndpointTests.StatusRemoval.cs:411]
- [x] [Review][Patch] The migration case gives the moved Tasks no Assignee, so "the Assignee is unchanged" (AC9) passes on `null` [tests/Yello.Tests.Slices/Projects/ProjectEndpointTests.StatusRemoval.cs:178]
- [x] [Review][Patch] Comment rule: open-question ids (Q1-Q10) in the class remarks [tests/Yello.Tests.Slices/Projects/ProjectEndpointTests.StatusRemoval.cs:27]
- [x] [Review][Patch] Comment rule: "under the story's recommended options" [tests/Yello.Tests.Client/FakeYelloApi.StatusRemoval.cs:9]
- [x] [Review][Patch] Comment rule: "this run's assumptions, from the story ... recorded in the Debug Log" [tests/Yello.Tests.Client/ProjectSettingsTests.Removal.cs:30]
- [x] [Review][Patch] Comment rule: a new comment says the removal rows are "pending until its dev phase maps the routes", which is narrative and already false [tests/Yello.Tests.Isolation/IsolationCases.cs]
- [x] [Review][Patch] Comment rule: test-design ids ("S-1's", "S-2") in prose, and "Pre-implementation every case here fails" narrative [tests/Yello.Tests.Slices/Projects/ProjectEndpointTests.StatusRemovalRace.cs, tests/Yello.Tests.Slices/Tasks/TaskEndpointTests.RemovedStatus.cs]
- [x] [Review][Defer] The done announcement says the client's count of moved Tasks, which can be stale when Tasks arrived under the lock; a recount of 0 after a `409` opens a "moves 0 Tasks" dialog [Yello.Client/Components/StatusDeltaEditor.Removal.cs:130] — deferred, low severity
- [x] [Review][Defer] An add-back answered `404` because another Admin already added the default back says "That Status is no longer in this Project." [Yello.Client/Components/StatusDeltaEditor.Removal.cs:181] — deferred, low severity
- [x] [Review][Defer] The removal copy has no singular ("moves 1 Tasks", "Remove and move 1 Tasks"), and `StatusAddedBack` drops the quotes its siblings put round the name [Yello.Client/Localisation/ClientCopy.resx:1787] — deferred, low severity
- [x] [Review][Defer] The Space defaults behind "Add back" are read once and never again, so a Space-level change leaves a missing or ghost entry until reload [Yello.Client/Components/StatusDeltaEditor.Removal.cs:61] — deferred, low severity
- [x] [Review][Defer] A direct removal answered `409 status-destination-required` whose re-read then fails, or no longer lists the Status, shows nothing [Yello.Client/Components/StatusDeltaEditor.Removal.cs:105] — deferred, low severity
- [x] [Review][Defer] A removal that never got an answer (network failure) is headed "Nothing was moved." although the server may have applied it [Yello.Client/Components/StatusDeltaEditor.razor:423] — deferred, low severity
- [x] [Review][Defer] A removal committed while another editor write is in flight is dropped with no message, the removal form of the 6.1 entry [Yello.Client/Components/StatusDeltaEditor.Removal.cs:111] — deferred, low severity
- [x] [Review][Defer] `MigrateTasksAsync` discards the `UPDATE`'s row count, so an occupant missing from the map (only possible for a writer that skipped the lock) surfaces later as a `547` on the owned definition's delete [Yello.Infrastructure/Persistence/ProjectStatusStore.cs:156] — deferred, low severity
- [x] [Review][Defer] Adding back a default the Space no longer has would delete the `Remove` and answer `200` with an unchanged set; unreachable until a Space-level removal exists [Yello.Application/Statuses/RestoreProjectStatus/RestoreProjectStatusHandler.cs:33] — deferred, low severity
- [x] [Review][Defer] The dialog's scope line and commit count Tasks from the last read, not the server's count at commit [Yello.Client/Components/StatusRemoveDialog.razor:29] — deferred, low severity

## Dev Notes

### The shape of the story in one paragraph

6.1 gave every Project a delta of `Add`, `Rename` and `Place` operations keyed by Status id, written by one writer under the Project's `ProjectTasks` lock, and made every consumer read the Project's set. Nothing could leave a set, so a Task's Status, once valid, stayed valid. 6.2 adds the only operation that takes a Status away: one request that, under that same lock and in one transaction, moves every Task out of the Status, re-anchors the Statuses placed after it, and records the removal (a `Remove` op for a Space default) or deletes the definition (for a Status the Project added). Two things make it safe beyond the endpoint itself: the Task writers must check the Status under the same lock (ledger `:1020`), and the S-2 statement must understand `Remove`, or the invariant test passes vacuously.

### Dominant finding: the invariant now depends on the lock, not only on the endpoint

- Before 6.2, a Task write that checked its Status before taking the lock was safe, because sets only grew. After 6.2 it is a real hole: an edit that read S as valid, then waited on the lock behind S's removal, would commit S after the removal migrated everything else. The removal's own transaction cannot catch it, because the edit commits after it.
- The fix is the order, not a second check: take the `ProjectTasks` lock, **then** read the effective set (a plain statement under RCSI reads the last committed state, which includes the removal), then validate, then write. `sp_getapplock` with a transaction owner is re-entrant, so the store's later take of the same lock is harmless.
- Creation already does this by accident of design: the Tasks-per-Project bound count takes the lock in the pipeline before the handler reads the set. That dependency is invisible in `CreateTaskHandler`; name it in a remark and prove it with a race case so a later change to the bound cannot silently reopen it.
- The removal decides occupancy under the lock too. A request without a destination that was "unoccupied" when the client read the counts is refused `409` if a Task arrived meanwhile, and a request with a destination migrates the newcomer. No Task is ever left behind.

### The removal, step by step (recommended; Q1, Q3)

Under `ProjectTasks(space, project)`, in the request's one transaction:

1. Read the defaults and the delta. Derive the set (`Before`).
2. Status not in `Before` → `404`. `Before.Count == 1` → `409 status-set-empty`.
3. If a destination is supplied: it must be in `Before` minus the removed Status (equivalently, the after-set) → else `400 invalid-status-destination`.
4. Count occupants (a plain statement after the lock). Occupied and no destination → `409 status-destination-required`.
5. Plan the delta change:
   - re-anchored `Place` ops for every placed Status, so the remaining order is `Before` minus the removed Status (AC10);
   - delete the removed Status's `Rename` and `Place` ops;
   - a Space default: add a `Remove` op;
   - an added Status: delete its `Add` op and its definition.
6. `Check` the after-set (never empty; names unchanged, so no `NameTaken` is possible except on add-back).
7. Write, in this order: migrate the Tasks (one set-based `UPDATE`), then the op deletes and inserts, then the owned definition's delete.
8. Re-read and answer the set with its Task counts.

**Add-back of a default:** under the lock, the id must have a `Remove` op in this Project's delta → else `404`. Delete the `Remove` op. `Check` for `NameTaken`. The default returns with its Space name at its Space-order slot: its old `Rename` and `Place` were deleted at removal.

**Why a `Remove` op for a default but a delete for an added Status (Q1):**
- A default must stay "removed in this Project" against later Space changes: FR-27 says a Space-level add or rename skips Projects that removed it, and 6.4 says a Space-level removal leaves them unaffected. That needs a row keyed on the id.
- An added Status exists only in this Project. Keeping its definition would keep its name in the owned-name unique index, so re-adding the same name would be refused, and no read surfaces a removed added Status to restore by id. Deleting it makes "add back" a plain add.

### Locking and races (RCSI)

- One lock for everything: `ProjectTasks(spaceId, projectId)`, exclusive. The removal never takes a Label lock or a second Project lock. Lock order stays: Project lock → Task rows → Labels.
- A Labels-only edit takes the Task row's `UPDLOCK` without the Project lock, then Label locks. A removal holding the Project lock and updating that row waits for it; the Labels edit never waits on the Project lock, so no cycle. State this in the store's remarks.
- Race cases to write (each holds one side open and waits on `LCK_M_%`, `TaskWire.WaitUntilBlockedOnALockAsync`):
  1. Removal of S holds; an edit to S queues → after commit, edit `400 invalid-task-status`.
  2. Same with a move.
  3. An edit into S holds (Status edit takes the lock); a removal of S without a destination queues → `409 status-destination-required`; with one → the edited Task is migrated.
  4. Removal of the first Status holds; a creation queues → the Task lands in the new first Status.
  5. Two removals of different Statuses where each is the other's destination → the second sees the first's result: `400 invalid-status-destination` (its destination is gone) and S-2 holds.

### Open questions

Each has a recommendation, which is the default. The dev phase decides each as a reversible, recorded decision, in Completion Notes and the Change Log. **None is marked [LEE]:** no rule of the spine changes (AD-16 gets an additive note), the only schema change is an index that a later migration can drop, and no RLS rule changes (no new table).

1. **What a removal stores** (AC7, AD-16). **(a) Recommended:** a `Remove` op for a Space default; for an added Status, delete its definition and all its ops. (b) A `Remove` op for both. Cost: the owned-name index keeps the name, so re-adding it is refused unless "add" learns to restore, and nothing surfaces removed added Statuses. (c) Delete-only for both. Cost: impossible for a default (other Projects use the row) and it would lose the "removed here" fact FR-27 needs.
2. **Route shapes, and how a default is added back** (AC4, AC7, AC14). **(a) Recommended:** `POST …/statuses/{statusId}/remove` `{ destinationStatusId: guid | null }` and `POST …/statuses/{statusId}/restore` (no body), both answering `200` with the set. The client finds removed defaults by comparing the existing Space read with the Project's set. The restore's path id must be a default this Project removed; anything else is the boundary `404`. (b) `DELETE …/statuses/{statusId}?destination=…`. Cost: a mapping hidden in a query string on a verb that suggests deletion, and a different body convention from `place`. (c) Restore by posting a removed default's name to the add route. Cost: an add whose meaning depends on the Space's names, and it cannot restore a default the Project had renamed.
3. **Order after removal and after add-back** (AC7, AC10; ledger `:1022`). **(a) Recommended:** re-anchor every placed Status to its predecessor in "`Before` minus the removed Status", as `Placements` does for a move; delete the removed Status's own `Rename`/`Place`; an added-back default reappears at its Space-order slot with its Space name. Keep the "ignored anchor" rule in `Apply` as a deterministic fallback for 6.3/6.4 (Space-level removal). (b) Leave dependent places alone (they are ignored once the anchor goes). Cost: a Status the Admin placed jumps back to its Space slot on someone else's removal. (c) Keep the removed default's rename and place for its add-back. Cost: an add-back that restores a name and slot the Admin no longer sees anywhere.
4. **Problem types** (AC1, AC2). **(a) Recommended:** `409 status-destination-required` (it depends on state: occupancy under the lock) and `400 invalid-status-destination` (the body names a Status that is not a valid destination, foreign or random alike). No count in the problem body: the client re-reads the set, whose `TaskCount` is the truth. (c) Reuse `invalid-task-status`. Cost: a different operation sharing a code makes the client's copy wrong.
5. **One Status per request** (AC1). **(a) Recommended:** one. FR-26's "a Task cannot be mapped onto a Status the same operation also removes" then reduces to "the destination is not the removed Status", which AC2 tests directly. 6.6 rejected a per-Status mapping table for the same transaction-size reason. (b) Several Statuses with a mapping each. Cost: scope no story asks for.
6. **How Edit and Move validate under the lock** (AC8; ledger `:1020`). **(a) Recommended:** `ITaskStore.LockProjectAsync`, called by the handler before it reads the set; re-read the Task's Project under it. (b) Pass the check into the store as a callback. Cost: Domain logic invoked from inside Infrastructure's transaction code, harder to read. (c) Re-check inside the store with SQL. Cost: a second derivation of the effective set (AD-16 says one).
7. **The commit with no destination chosen** (AC14). **(a) Recommended:** the commit stays enabled; pressing it with the placeholder selected shows the field's message ("Choose where these Tasks go.") and sends nothing. That follows the project's form pattern and keeps a focusable control. (b) A disabled commit with a gate line, as the mockup draws for the Space-level dialog. Cost: a disabled control that says why only beside it; acceptable, but not the project's form pattern.
8. **A write that answers `404` because the Status left the set** (AC14). **(a) Recommended:** re-read; if the read succeeds, say so inline and render the new set; only a failing read is a page refusal. This also fixes the same case for 6.1's rename and place, which today blank the page. (b) Keep 6.1's behaviour. Cost: another Admin's removal turns this Admin's page into "Not available".
9. **The `Sequence` backstop** (ledger `:1032`). **(a) Recommended:** a unique index `(SpaceId, ProjectId, Sequence)` in a new migration. Every writer computes `NextSequence` under the lock and deletes before it inserts, so nothing should collide; the removal's re-anchoring is the heaviest user. (b) Defer again. Cost: a writer that skips the lock could tie two ops with no signal.
10. **List cursor and pager** (ledger `:655`, `:676`). **(a) Recommended:** a Status-sort cursor whose Status is not in the order answers `400 invalid-list-cursor` (the cursor carries only the Status id, so "resume at the next Status" cannot know where it was); the count needs no change, because by S-2 no Task holds a Status outside the order. The pager clears a selection that is not a column. Fix the `PagerTabs` allocation only if trivial; otherwise re-own that half to **sweep** (BoardView is at its S1200 dependency cap). (b) Encode the Status's ordinal in the cursor. Cost: a cursor format change for a rare case.
11. **The 5,000-Task case** (AC16). **(a) Recommended:** one `Nightly` tiered case (`Explicit = true`), seeded through the product or the existing bulk seeding helper of story 2.9, asserting one transaction (S-2 holds and the count moved is 5,000) and recording the duration in the log. No duration assertion (no NFR states one for 6.2; 6.4's bound is larger and is 6.4's). (b) A PR-stage case. Cost: minutes on every run.
12. **An unoccupied removal's confirmation** (AC5, AC14). **(a) Recommended:** none: it removes directly and announces, because nothing is lost (a default can be added back with its id; an added name can be added again). (b) A confirm for every removal. Cost: friction the PRD's "succeeds directly" argues against.

### Contradictions found (recorded; do not resolve beyond what is stated)

- **C1.** `EffectiveStatusSet`'s remarks say an anchor that leaves "needs nothing from whoever removed the anchor". Q3 (a) makes the removal re-anchor. Rewrite the sentence; keep the fallback.
- **C2.** `StatusDeltaRow.razor`'s remarks end "No remove control." Remove that sentence.
- **C3.** `WireDatabase.TasksOutsideTheirProjectsSetSql`'s remarks say "with no operation that removes a Status". Rewrite them with the statement.
- **C4.** The mockup (`project-settings.html` §3, §4) shows the **Space-level** removal dialog and a disabled commit. 6.2's dialog is the Project-level analogue: one destination, no per-Project table. The disabled commit is Q7.
- **C5.** `EXPERIENCE.md:268` puts "Delete Project" in Project settings; 6.1's Q9 kept it on Space home. 6.2 does not touch it.

### Ledger additions (the dev phase writes these, if they stay true)

- Q8 (b), if taken: "A delta write's `404` for a Status that left the set renders the page refusal" (low, UX).
- Q10's `PagerTabs` allocation half, if not fixed (low, sweep).
- Anything the S-1 or race cases find that is not fixed here.

### Architecture compliance (must)

- **AD-21 ring** (`tests/Yello.Tests.Architecture/AllowedReferenceEdges.cs`): kind and pure planning in `Yello.Domain/Statuses/`; slices in `Yello.Application/Statuses/<Slice>/`; SQL in `Yello.Infrastructure/Persistence/`; endpoints in `Yello.Host/Endpoints/`; contracts in `Yello.Contracts/Projects/`. No EF type in Domain or Application; no ASP.NET type in Application or Domain.
- **AD-1 / AR-3:** no Role read, no Membership check in a slice.
- **AD-2:** no `Where SpaceId`; raw SQL only in Infrastructure, parameters only, taking the lock by its `ApplicationLocks` name.
- **AD-3 / AD-20:** an in-Space not-found answers the boundary body and writes no refusal record.
- **AD-15:** no sibling's key changes; migrated keys come from the destination column's last key, under the lock.
- **AD-16:** one derivation; no materialised set; `Remove` keyed by id.
- **AD-17:** one transaction; S-2 after every case; no endpoint removes without its mapping.
- **AD-18:** idempotent through the pipeline.
- **AD-19 / FR-37 (C-1, C-2):** additive contract changes only.
- **AR-34:** problem+json with stable `urn:yello:problem:*` codes; application-generated ids; UTC.
- **Code comments** (CLAUDE.md): why, not what; no story numbers, AC/Q/ledger ids, dates or history; no XML doc that restates a name.

### Traps

- **S1135 (ledger `:114`).** The first default Status name may never appear in a comment, doc comment or Razor comment. Write "the first default" in prose and `DefaultStatusSet.Names[0]` in code. Test string literals are fine.
- **NO ACTION keys everywhere.** `Task → StatusDefinition`, `StatusDeltaOperation.StatusId` and `.AfterStatusId → StatusDefinition` are all NO ACTION. Deleting an owned definition before its Tasks and before the ops that name it (including as an anchor) fails 547. Order: Tasks, ops, definition.
- **The unique key index on Tasks.** Generating keys in a loop and saving tracked entities for 5,000 rows is slow and easy to get wrong; one set-based statement with precomputed keys is the shape. Keys must be strictly increasing after the destination's last key.
- **S-2 passing vacuously.** If the S-2 statement does not know `Remove`, a removal that strands Tasks still reports 0. Plant 1 exists to catch exactly this.
- **S1200 / S1541** split classes in 5.2, 5.4 and 6.1 (`BoardView` is at its dependency cap; the editor is 413 lines). Plan new components and a sibling endpoints file.
- **The editor's write path.** It drops a write while another is in flight and reuses an unanswered key (ledger `:1035`). The removal is long-running: the progress region must make the in-flight state visible, and a second press must not send a second removal.
- `TreatWarningsAsErrors`: 0 warnings is a gate. `--no-build` runs stale binaries; rebuild after every change. A leftover `Yello.Host.exe` locks `bin/Debug` (`Get-Process Yello.Host`).

### ATDD artifacts - red-phase scaffolds already in the tree

Checklist: `_bmad-output/test-artifacts/atdd-checklist-6-2-remove-a-status-from-a-project-by-migrating-its-tasks.md` (scenario map, seed constraints, findings).

| File | Cases | ACs | Today |
|---|---|---|---|
| `tests/Yello.Tests.Slices/Tasks/TaskEndpointTests.RemovedStatus.cs` | 4 | AC8, AC15 | red (skipped) |
| `tests/Yello.Tests.Slices/Projects/ProjectEndpointTests.StatusRemovalRace.cs` | 5 | AC1-AC3, AC8 | 4 red; **1 green**: `The_S2_statement_counts_a_Task_whose_Status_its_own_Project_removed_and_nothing_else` |
| `tests/Yello.Tests.Slices/Projects/ProjectEndpointTests.StatusRemoval.cs` | 15 | AC1-AC3, AC5-AC7, AC9-AC13, AC15 | red (skipped) |
| `tests/Yello.Tests.Slices/Projects/ProjectEndpointTests.StatusRemovalScale.cs` | 1 (Nightly, Explicit) | AC16 | red (skipped) |
| `tests/Yello.Tests.Slices/Statuses/EffectiveStatusSetRemovalTests.cs` | 7 (no database) | AC2, AC5-AC7, AC10 | red (skipped) |
| `tests/Yello.Tests.Architecture/RefusalBoundaryGateTests.StatusRemoval.cs` | 6 | AC1, AC2, AC4, AC7, AC11, AC12, Task 2 | 5 red; **1 green**: `Every_delta_kind_fits_the_column_it_is_stored_in` |
| `tests/Yello.Tests.Isolation/IsolationCases.StatusRemoval.cs` | 11 rows | AC11, AC12 | pending until 6.2 (GATE-003: live before review) |
| `tests/Yello.Tests.Client/ProjectSettingsTests.Removal.cs` | 8 | AC7, AC14 | red (skipped) |

Supporting: `tests/Yello.Tests.Slices/Projects/StatusRemovalWire.cs`, `tests/Yello.Tests.Client/FakeYelloApi.StatusRemoval.cs` (hooked in `FakeYelloApi.SpaceDeletion.cs`; Space defaults seeded by `SeedProjectStatuses`), stand-ins in `tests/Yello.Tests.Shared/ProjectStatusWire.cs`, `WireDatabase.DeltaOperationCountInAsync`. **`WireDatabase.TasksOutsideTheirProjectsSetSql` already knows `Remove`** (Task 8's first bullet, C3); plant 1 was run: with the `Remove` clause removed the S-2 gate failed `Expected 1, Actual 0`, then was restored. The two green cases are standing regression gates.

Activation order, riskiest assumption first:

1. S-2 under the edit/move-versus-removal race with the lock taken before the Status check: confirm the S-2 gate is green, then (with `Remove` in the domain) `An_edit_to_a_Status_queued_behind_its_removal_is_refused_400_invalid_task_status` and `A_move_to_a_Status_queued_behind_its_removal_is_refused_400_invalid_task_status` - each must first fail with `200` and an S-2 count of 1, then pass with Task 4's `LockProjectAsync`; then `A_creation_queued_behind_the_removal_of_the_first_Status_lands_in_the_new_first_Status`.
2. `EffectiveStatusSetRemovalTests`; `The_delta_kinds_are_add_rename_place_and_remove`; `The_delta_sequence_is_unique_per_Project_in_the_model`.
3. The problem-type, route and AC4 gates (amend ARCH-503, `The_Project_Status_routes_declare_...` and `No_Project_request_contract_carries_anything_but_a_name` in the same change; swap the stand-ins).
4. The removal cases in `StatusRemoval.cs` (unoccupied, occupied, 409, destinations, set-empty, added Status).
5. `StatusRemovalRace.cs`: occupancy under the lock (both variants), crossed removals, S-1.
6. Order and add-back; 7. role, boundary, replay, deletion, then the 11 isolation rows live; 8. readers (removed Status refused, List cursor); 9. the browser cases, then the Nightly case by hand.

Expect these active cases to go red by design and amend them deliberately: ARCH-503, `The_Project_Status_routes_declare_their_capability_serve_both_surfaces_and_consult_no_bound`, `No_Project_request_contract_carries_anything_but_a_name`, isolation route coverage, and `ProjectSettingsTests.The_Board_links_an_Owner_to_the_editor_which_names_each_Statuses_source` (asserts no Remove button).

Activate one at a time, confirm it fails for the stated reason, then implement. A scaffold that is wrong is corrected and the correction recorded in the Debug Log, not deleted.

### Library / framework

No new package or project; no web research needed. Every API used is already in the codebase.

| Area | What it uses |
|---|---|
| EF Core 10.0.11 | `ExecuteDeleteAsync`, `ExecuteUpdateAsync`, `Database.ExecuteSqlAsync` / `SqlQuery` with interpolated parameters, a filtered or plain unique index, a hand-checked migration |
| SQL Server 2025 | RCSI, `sp_getapplock` (transaction owner, re-entrant), `OPENJSON` |
| ASP.NET Core 10.0.11 | Minimal APIs, Asp.Versioning.Http 10.0.0 |
| Blazor WebAssembly 10.0.11 | The client; Localization 10.0.11 for copy |
| Tests | xunit.v3 4.0.0, Testcontainers 4.6.0, Playwright 1.62.0, ArchUnitNET 0.13.3 |

### Testing requirements

Follow `tests/TESTING-CONVENTIONS.md`: traits `:44-95`, rules `:97-207` (races `:102-180`), browser cases `:209-305`, migrations `:599-620`, the shared container `:621-690`, the zero-tests trap `:706`, isolation `:790-911`, tiers `:945-960`.

- **Traits.** `Suite`, `Priority`, `Requirement`, `Tier`. S-1, S-2, isolation and the race cases are **P0**. `Requirement` values: FR-26, FR-25, FR-16, AD-15, AD-16, AD-17, AR-24, NFR-1, UX-DR16.
- **No new container-backed test class.** Slices cases in new partials of existing classes: `ProjectEndpointTests.StatusRemoval.cs`, `ProjectEndpointTests.StatusRemovalRace.cs`, `TaskEndpointTests.RemovedStatus.cs` (or equivalents). Handler and domain tests use no database. Isolation rows in `IsolationCases.ProjectStatuses.cs`. The browser cases extend `ProjectSettingsTests.cs` (fake API) and add a pager case to `SmallViewportTests`.
- **Seed through the product.** Raw SQL only for catalogue reads, S-2, the S-1 trigger and the key-order assertions.
- **Every absence has an anchor and is seen red under a plant.** Restore each plant in a `finally` from saved content; never `git checkout --`, `git restore` or `git reset`.
- **Memory budget.** One `dotnet test` at a time. Iterate on one project filtered to the affected classes. Develop ends with one background full run with `--max-parallel-test-modules 1` and `-p:YelloSkipBrowserSuite=true`.

**Minimum cases (Slices):**

| # | Case |
|---|---|
| 1 | Remove an unoccupied default: the set drops it, the Board has one column fewer, the Space read is unchanged (same ids, names). |
| 2 | Remove an unoccupied added Status: its definition row is gone; adding the same name again succeeds with a new id. |
| 3 | Remove an occupied Status with a destination: every Task moved, at the end of the destination column in source order; their ids, titles, Assignees, Labels and due dates unchanged; no destination sibling's key changed. |
| 4 | Occupied, no destination → `409 status-destination-required`; nothing changed. |
| 5 | Destination = the removed Status, random, another Space's, a sibling Project's added Status, an already removed default → one identical `400 invalid-status-destination`; nothing changed. |
| 6 | Unoccupied with a valid destination → succeeds; with an invalid one → `400`. |
| 7 | The last Status → `409 status-set-empty`. |
| 8 | Add back a removed default: same id, Space name, Space slot; a Task can be created into or moved to it again. Add back with a name clash → `409 status-name-taken`. Add back an id that is not a removed default → `404`. |
| 9 | Order of the rest is unchanged when the removed Status anchors other places. |
| 10 | S-1: a trigger forces a failure after the migration statement → `5xx`, nothing changed, S-2 holds; anchored on success without the trigger. |
| 11 | The five race cases (Locking and races). |
| 12 | Member and Viewer `403` on both routes, both surfaces. |
| 13 | Unknown Project, foreign Status in the path → `404`, byte-identical. |
| 14 | Idempotent replay of a removal: same response, nothing migrated twice. |
| 15 | Delete a Project that has removed a default and holds Tasks; delete a Space holding it. |
| 16 | The List View: a Status-sort cursor on a removed Status → `400 invalid-list-cursor`; a fresh walk returns every Task once. |
| 17 | Task edit and move to a removed Status → `400 invalid-task-status`; Board column page for it → `404`. |
| 18 | S-2 after every case. |
| 19 | (Nightly) 5,000 Tasks migrated in one transaction. |

**Minimum plant table** (restore each one in a `finally`):

| # | Plant | Expected red |
|---|---|---|
| 1 | The S-2 statement ignores `Remove` | a case that strands Tasks (plant 2) passes; with the real statement it fails |
| 2 | The removal skips the Task migration | cases 3, 10 and S-2 |
| 3 | `Apply` ignores `Remove` | cases 1, 2, 8, 9 and the unit tests |
| 4 | Occupancy read before the lock | race case 3 |
| 5 | Edit validates before the lock again | race case 1 |
| 6 | Move validates before the lock again | race case 2 |
| 7 | Destination checked against `Before` instead of the after-set | case 5 (the removed Status as destination) |
| 8 | Dependent places not re-anchored | case 9 |
| 9 | Migrated keys computed from the source keys / no fresh last key | case 3 (order or uniqueness) |
| 10 | The migration and the delta write in separate transactions | case 10 |
| 11 | The client picker pre-selects the first Status | the browser no-default case |
| 12 | Remove request's destination member removed or a second removal path added | the AC4 gate |

**Browser cases (fake API):** remove control absent for Member/Viewer; unoccupied removal announces and the row is gone; occupied opens the dialog with "Choose…" and no Status selected, lists only post-removal Statuses, refuses an empty commit locally; in flight the `role="status"` region has focus and shows no percentage; done moves focus to the destination row; a `409` re-reads and opens the dialog; a refusal says "Nothing was moved."; "Add back" lists removed defaults and restores one; the Board and the pager follow; 320px usable.

### Previous story intelligence (6.1)

- **Shape that works:** one writer (`ProjectStatusDeltaWriter`) that locks, reads under the lock, plans on a draft, checks the after-set, writes, and answers the set. Put the removal through it.
- **Review finding that matters here:** a place written against an order that later changes replays wrongly. 6.1 fixed it by re-anchoring every placed Status left to right (`EffectiveStatusSet.Placements`). The removal changes the order too; use the same technique.
- **Owned definition names:** renaming an added Status also writes its definition's name, so the owned-name index tracks the shown name. Deleting the definition on removal keeps that index honest.
- **Scaffolds corrected in 6.1:** catalogue `STRING_AGG` needs `COLLATE DATABASE_DEFAULT`; read over the wire inside the host block; `GetCollation()` needs the design-time model; isolation rows must not push B past its 50-Project bound (seed the foreign side once per world); every Token-reachable route needs an `ISO-TOKEN-OWN-OTHER-SPACE-*` row.
- **Gates that went red by design in 6.1:** `SpaceIsolationSchemaTests` (new tables), ARCH-503, the role-matrix theory, `No_Project_request_contract_carries_anything_but_a_name` (the Project Status requests share the namespace; it lists them and forbids a Space or `ProjectId` member). Expect the last one to need the new request added to its list.
- **Plants:** run them in small groups; a host killed for low memory skips the harness's `finally`, so check for residue (`git status`) and restore by hand from saved content. Two 6.1 plants did not compile under the analyzers and proved nothing: make a plant compile.
- **Dev choices to keep:** the effective-set source is a static extension on `IStatusDefinitionReader` (no cache); the "Project settings" link lives in `ViewToggle`; refusals are mapped in `ProjectStatusAnswers`.
- **Deferred 6.1 client issues that 6.2 brushes against** (owner UX/sweep; do not fix unless trivial and on the path): the rename field closes before validation (`:1034`), a write in flight drops another silently (`:1035`), a stale anchor (`:1036`), a `null` body leaves the editor loading (`:1037`).

### Git intelligence

- `6c6eb9a` story 6.1 file, `bca3a5f` its ATDD scaffolds, `221b03e` its implementation, `4eb4b5f` its review close (re-anchoring fix). `7f57e98` closed epic 5.
- The cycle per story: story file → ATDD scaffolds (`Skip = "red: ..."`, `atdd-checklist-<key>.md`, `PendingUntil` isolation rows live before review) → implementation → review. The orchestrator commits each phase.
- Latest migration: `20261121155445_ProjectStatusDelta`. Generate any new one after it.

### Project Structure Notes

- **New (expected):**
  - Application: `Yello.Application/Statuses/RemoveProjectStatus/`, `Yello.Application/Statuses/RestoreProjectStatus/`.
  - Infrastructure: a migration for the `Sequence` index (Q9).
  - Contracts: `Yello.Contracts/Projects/RemoveProjectStatusRequest.cs`.
  - Client: `Yello.Client/Components/StatusRemoveDialog.razor`, a removed-defaults list component (names are the dev's).
  - Tests: as listed under Testing requirements.
- **Updated (expected):**
  - Domain: `StatusDeltaOperation.cs` (`Remove`, remarks), `EffectiveStatusSet.cs`, `ProjectStatusDelta.cs`, `IProjectStatusStore.cs`, `Yello.Domain/Tasks/ITaskStore.cs` (`LockProjectAsync`).
  - Application: `ProjectStatusDeltaDraft.cs`, `ProjectStatusDeltaWriter.cs`, `ProjectStatusSet.cs` (outcomes), `EditTaskAttributesHandler.cs`, `MoveTaskHandler.cs`, `CreateTaskHandler.cs` (remark), `ViewListHandler.cs`.
  - Infrastructure: `ProjectStatusStore.cs`, `TaskStore.cs`, `ListReader.cs`, `StatusDeltaOperationConfiguration.cs`, the model snapshot, `SpaceContentStores.cs` if registration changes.
  - Contracts: `ProjectStatusRoutes.cs`, `ProblemTypes.cs`.
  - Host: `ProjectStatusEndpoints.cs` (or a split), `ProjectStatusAnswers.cs`, `Program.cs`.
  - Client: `StatusDeltaEditor.razor`, `StatusDeltaRow.razor`, `BoardView.Pager.cs`, `RefusalCopy.cs`, `ClientCopy.resx`, `components.css`.
  - Tests: `WireDatabase.cs`, `RefusalBoundaryGateTests.Delete.cs` (ARCH-503), `RefusalBoundaryGateTests.ProjectStatuses.cs`, `RefusalBoundaryGateTests.Projects.cs` (request list), `ActiveSpaceEndpointTests.RoleMatrix.cs`, `IsolationCases.ProjectStatuses.cs`, `FakeYelloApi.ProjectStatuses.cs`, `ProjectSettingsTests.cs`, `EffectiveStatusSetTests.cs`, `ProjectStatusWriteHandlerTests.cs`, `EditTaskAttributesHandlerTests.cs`, `MoveTaskHandlerTests.cs`.
  - Docs: `deferred-work.md`, `sprint-status.yaml`, the AD-16 note and ER edge in `ARCHITECTURE-SPINE.md`.

### References

- `epics.md`: `:212` (AR-24), `:254` (UX-DR16), `:381` (coverage), `:469-477` (epic 6 notes), `:2163-2205` (this story), `:2207-2288` (6.3, 6.4 for the boundary of scope).
- `prd.md`: `:458` (§4.7), `:471-480` (FR-25), `:482-490` (FR-26), `:492-506` (FR-27, for what a `Remove` op must mean to the Space level).
- `ARCHITECTURE-SPINE.md`: AD-2, AD-3, AD-15, AD-16 (`:186-191`), AD-17 (`:193-197`), AD-18, AD-21, the ER (`:392-412`).
- `EXPERIENCE.md`: `:215`, `:257-260`, `:331`, `:339-364`, `:392-405`.
- `DESIGN.md`: `:223-229` (dialog), `:256-262` (picker), `:322`, `:468-476`, `:501`.
- Mockup: `ux-designs/ux-YelloBMAD-2026-08-18/mockups/project-settings.html` §1, §3, §4.
- `test-design-qa.md`: `:302-303` (A-4, A-5), `:314` (P-10), `:318-319` (S-1, S-2).
- `deferred-work.md`: `:114`, `:655`, `:676`, `:1020`, `:1022`, `:1030-1041`.
- Code: `EffectiveStatusSet.cs`, `StatusDeltaOperation.cs`, `ProjectStatusDelta.cs`, `IProjectStatusStore.cs`, `ProjectStatusDeltaWriter.cs`, `ProjectStatusDeltaDraft.cs`, `ProjectStatuses.cs`, `ProjectStatusStore.cs`, `StatusDeltaOperationConfiguration.cs`, `ProjectStore.cs:98-157`, `TaskStore.cs:149-202`, `:391-451`, `:520-536`, `BoundCounter.cs:128-140`, `ApplicationLocks.cs:57-64`, `EditTaskAttributesHandler.cs:48-82`, `MoveTaskHandler.cs:38-69`, `CreateTaskHandler.cs:47-58`, `ListReader.cs:112-156`, `ProjectStatusRoutes.cs`, `ProjectStatusEndpoints.cs`, `ProblemTypes.cs`, `ProblemResponse.cs`, `StatusDeltaEditor.razor`, `StatusDeltaRow.razor`, `BoardView.Pager.cs`, `WireDatabase.cs:805-817`, `ActiveSpaceEndpointTests.DeleteSpace.cs:268-305`, `:774-800`, `IsolationCases.ProjectStatuses.cs`, `IsolationCases.cs:773-790`.

## Dev Agent Record

### Agent Model Used

Claude Opus 5.5 (claude-opus-5-5), bmad-dev-story, unattended develop phase.

### Debug Log References

- Activation order followed. RACE-1/RACE-2 (`TaskEndpointTests.An_edit_...` / `A_move_to_a_Status_queued_behind_its_removal_...`) were activated with `Remove` in the domain and failed for the stated reason (`200` with the removed Status written), then passed once edit and move take `ITaskStore.LockProjectAsync` before reading the set. The creation race passed with no creation change, as predicted.
- Scaffold corrected: `ProjectEndpointTests.Removing_an_unoccupied_default_...` compared `ProjectStatusEntry` records with `Assert.Equal(entries, read)`; the record's `Members` list compares by reference, so equal sets failed. Now compares a projection of every member (`Shown`).
- Scaffold corrected: `ProjectSettingsTests.Removing_an_unoccupied_Status_removes_it_directly_and_announces_it` waited for `.board-column` `Nth(2)`, which the Board's loading skeleton satisfies before any heading exists; it now waits for the third column heading.
- Stand-ins swapped for contract members and deleted: `ProjectStatusWire.Remove`/`Restore`/`RemoveOf`/`RestoreOf`/`StatusDestinationRequired`/`InvalidStatusDestination`; the gates' string names (`RemoveProjectStatusRequest`, the two problem types); `EffectiveStatusSetRemovalTests.Remove`.
- Gates expected red by design and amended in the same change: ARCH-503 (widened by exactly `POST …/remove` and `POST …/restore`), `No_Project_request_contract_carries_anything_but_a_name` (lists `RemoveProjectStatusRequest`), `ProjectSettingsTests.The_Board_links_an_Owner_to_the_editor_...` (now four `Remove` controls, still no bare `×`). `The_Project_Status_routes_declare_...` matches its patterns exactly and did not go red. The 11 isolation rows are live (`PendingUntil` removed).
- Scaffold corrected: `FakeYelloApi.ReleaseStatusRemoval` completed `_heldStatusRemoval`, which the held request had already exchanged to `null`, so a held removal was never released and the in-flight case timed out; the release now keeps its own reference.
- Full run (once, without the browser suite): 2283 total, 2275 passed, 0 failed, 8 skipped; log `artifacts/test-runs/6-2-remove-a-status-from-a-project-by-migrating-its-tasks-develop.log`. Filtered runs: `ProjectEndpointTests` (94), the four removal/reader methods, the 31 queued races, `IsolationCaseTests` + `IsolationCaseListTests` (476/476), `ProjectSettingsTests` (17/17), Architecture (534/534).
- Nightly `A_removal_migrating_five_thousand_Tasks_commits_as_one_transaction` run once by hand (`--explicit only`): passed.
- Plants, one at a time, each a filtered run of the class that should catch it, original saved and restored in a `finally` (scratchpad `plant.ps1`; restoration checked by content and `git status`):
  - Plant 2, writer skips `MigrateTasksAsync`: caught by `Removing_an_occupied_Status_moves_every_Task_...` (`Expected 4, Actual 1`). Restored.
  - Plant 3, `Apply` ignores `Remove`: caught by 7 of 9 `EffectiveStatusSetRemovalTests`. Restored.
  - Plant 5, edit checks the set without `LockProjectAsync`: caught by `An_edit_to_a_Status_queued_behind_its_removal_...`. Restored.
  - Plant 6, move checks the set without `LockProjectAsync`: caught by `A_move_to_a_Status_queued_behind_its_removal_...`. Restored.
  - Plant 7, destination checked against the set before the removal: caught by `Every_destination_outside_the_set_after_the_removal_...`. Restored.
  - Plant 8, dependent places not re-anchored: caught by `Removing_a_Status_that_others_are_placed_after_...`. Restored.
  - Plant 12, a second Project request carrying `DestinationStatusId`: caught by `A_Status_leaves_a_Project_only_through_the_remove_route_...`. Restored. (A first attempt did not compile under the analyzers and proved nothing; it was reformatted and rerun.)
  - Plant 1 was proved by the ATDD phase. Not planted in the develop phase (all four were run at code review, below): 4 (occupancy before the lock - the read is a single call after `LockAsync` in the writer; the two occupancy races cover it), 9 (keys from the source keys), 10 (separate transactions - the unit of work is the pipeline's; S-1 covers it) and 11 (client picker preselect - a browser plant run was not affordable in the memory budget). None was killed for memory.
- A build started while a filtered Slices run held `Yello.Host.dll` failed to copy; it was rerun after the run ended. No plant was in place.
- Code review, plants 4, 9, 10 and 11, one at a time, each a filtered run of the class that should catch it after `dotnet build-server shutdown`, the original saved as bytes and restored in a `finally` (restoration checked byte for byte, then by `git status` and a search for the plant's text):
  - Plant 4, occupancy counted before the Project's lock (the writer read `CountTasksByStatusAsync` before `LockAsync` and fed the draft from it): caught by `A_removal_without_a_destination_queued_behind_an_edit_into_the_Status_is_refused_409`, which answered `200` and stranded the edited Task. A first attempt did not compile (S3257, an unused lambda parameter) and proved nothing; it was corrected and rerun.
  - Plant 9, migrated keys computed from the source keys (`PositionKey.After` of each Task's own key): caught by `Removing_an_occupied_Status_moves_every_Task_to_the_end_of_the_destination_in_source_order`, which found the moved Tasks before the resident Task.
  - Plant 10, the migration committed on a separate connection (its own `SESSION_CONTEXT`, auto-commit) while the delta write stayed in the request's transaction: caught by `A_failure_forced_after_the_migration_rolls_the_whole_removal_back`, which found every Task in the destination after the forced failure; the occupied-removal case passed under the same plant, so the plant migrated.
  - Plant 11, the destination picker pre-selecting the first Status (`_chosen ??= Destinations.FirstOrDefault()?.Id`): caught by `An_occupied_Status_opens_a_dialog_with_no_destination_chosen_and_only_the_Statuses_left`, which read a Status id where it expects the empty placeholder.
- Code review patches: the new browser case `Removing_the_only_Status_opens_no_dialog_and_is_refused_inline` first timed out in `OpenSettingsAsync`, which waits for a fourth row; it now waits for its one row.

### Completion Notes List

- **Domain.** `StatusDeltaKind.Remove`; `EffectiveStatusSet.Apply` drops a removed Status; `EffectiveStatusSet.Reanchored` re-anchors every placed Status for a removal, sharing `Placements`' left-to-right anchoring (no third ordering algorithm); `ProjectStatusDelta.Without` / `WithoutRemovalOf` / `Removes` describe the delta after a removal or an add-back so `Check` sees the real after-set. `IProjectStatusStore` gains `MigrateTasksAsync`, `DeleteOperationsAsync`, `DeleteOwnedDefinitionAsync`; `ITaskStore` gains `LockProjectAsync`.
- **Application.** `RemoveProjectStatusHandler` and `RestoreProjectStatusHandler` go through `ProjectStatusDeltaWriter` (lock, read, plan, check, write, answer the set), which gained an async-plan overload so occupancy is counted under the lock (`draft.OccupantsAsync`). Decision order: not in set `404`; set of one `409 status-set-empty`; destination that is the removed Status or outside the set `400 invalid-status-destination`; occupied with no destination `409 status-destination-required`. The writer records in NO ACTION order: migrate, delete the removed Status's operations, write the re-anchored places and the `Remove`, then delete an owned definition. Edit and move lock the Project before reading the set and refuse a Task whose Project changed under the lock.
- **Infrastructure.** The migration is one `UPDATE … JOIN OPENJSON` with keys computed from the destination's last key in the source column's BIN2 order. Migration `20261121190936_StatusDeltaSequenceUnique` (hand-checked, expand only).
- **Host/Contracts.** `POST …/statuses/{id}/remove` (`RemoveProjectStatusRequest(Guid? DestinationStatusId)`) and `POST …/statuses/{id}/restore`, both `ConfigureProjectStatusDelta`, both surfaces, answering `200` with the set (`ProjectStatusRemovalEndpoints`, split for size). `ProblemTypes.StatusDestinationRequired` / `InvalidStatusDestination`. A Status-sort List cursor on a Status outside the order answers `400 invalid-list-cursor`.
- **Client.** Remove per row; `StatusRemoveDialog` (scope line, native `select` with an empty "Choose…" first option and no default, local message on an empty commit, in-flight `role="status"` region that takes focus, no percentage); done announces and focuses the destination row's Remove control (the list heading for a direct removal); refusals say "Nothing was moved." plus the reason; a direct removal answered `409 status-destination-required` re-reads and opens the dialog; a write's `404` re-reads and says "That Status is no longer in this Project." inline, raising the page refusal only when the read fails (fixes 6.1's rename/place too); `StatusRestoreList`/`StatusRestoreItem` list removed defaults from the Space read with "Add back". The pager forgets a selection that is no longer a column on a Board read again.
- **Decisions (each reversible; cut any line to revisit):**
  - Q1 (a): a `Remove` operation for a Space default; an added Status's definition and operations are deleted. Reason: FR-27 needs "removed here" keyed on the id, and the owned-name index must release an added name.
  - Q2 (a): `POST …/remove` with `{ destinationStatusId }` and `POST …/restore` with no body. Reason: a body-borne mapping, the same convention as `place`.
  - Q3 (a): re-anchor every placed Status; delete the removed Status's own rename and place; the ignore rule stays as the fallback. Reason: no Status jumps on someone else's removal.
  - Q4 (a): `409 status-destination-required` and `400 invalid-status-destination`, no count in the body. Reason: the set's `TaskCount` is the truth.
  - Q5 (a): one Status per request.
  - Q6 (a): `ITaskStore.LockProjectAsync` before the set read; a Task whose Project changed under the lock is `404`. Reason: one derivation, no callback into Infrastructure.
  - Q7 (a): the commit stays enabled; an empty commit shows "Choose where these Tasks go." and sends nothing.
  - Q8 (a): a write's `404` re-reads before it is believed.
  - Q9 (a): unique `(SpaceId, ProjectId, Sequence)`.
  - Q10 (a): the stale Status-sort cursor is `400 invalid-list-cursor`; the pager selection is cleared; the `PagerTabs` allocation re-owned to sweep.
  - Q11 (a): one Nightly, Explicit case, no duration assertion.
  - Q12 (a): an unoccupied Status is removed directly with no confirmation.
  - Not in the story's options: the add-back's "Choose…"-free list and the destination dialog are separate components (`StatusRestoreList`, `StatusRestoreItem`, `StatusRemoveDialog`) and the editor's removal half lives in `StatusDeltaEditor.Removal.cs`, to keep the editor under its analyzer caps.

### File List

- `_bmad-output/implementation-artifacts/deferred-work.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`
- `_bmad-output/planning-artifacts/architecture/architecture-YelloBMAD-2026-08-17/ARCHITECTURE-SPINE.md`
- `tests/Yello.Tests.Architecture/RefusalBoundaryGateTests.Delete.cs`
- `tests/Yello.Tests.Architecture/RefusalBoundaryGateTests.Projects.cs`
- `tests/Yello.Tests.Architecture/RefusalBoundaryGateTests.StatusRemoval.cs`
- `tests/Yello.Tests.Client/FakeYelloApi.StatusRemoval.cs`
- `tests/Yello.Tests.Client/ProjectSettingsTests.cs`
- `tests/Yello.Tests.Client/ProjectSettingsTests.Removal.cs`
- `tests/Yello.Tests.Isolation/IsolationCases.StatusRemoval.cs`
- `tests/Yello.Tests.Isolation/IsolationCases.cs`
- `tests/Yello.Tests.Shared/ProjectStatusWire.cs`
- `tests/Yello.Tests.Slices/Projects/ProjectEndpointTests.StatusRemoval.cs`
- `tests/Yello.Tests.Slices/Projects/ProjectEndpointTests.StatusRemovalRace.cs`
- `tests/Yello.Tests.Slices/Projects/ProjectEndpointTests.StatusRemovalScale.cs`
- `tests/Yello.Tests.Slices/Projects/StatusRemovalWire.cs`
- `tests/Yello.Tests.Slices/Statuses/EffectiveStatusSetRemovalTests.cs`
- `tests/Yello.Tests.Slices/Statuses/ProjectStatusWriteHandlerTests.cs`
- `tests/Yello.Tests.Slices/Tasks/AssignTask/AssignTaskHandlerTests.cs`
- `tests/Yello.Tests.Slices/Tasks/CreateTask/CreateTaskHandlerTests.cs`
- `tests/Yello.Tests.Slices/Tasks/DeleteTask/DeleteTaskHandlerTests.cs`
- `tests/Yello.Tests.Slices/Tasks/EditTaskAttributes/EditTaskAttributesHandlerTests.cs`
- `tests/Yello.Tests.Slices/Tasks/MoveTask/MoveTaskHandlerTests.cs`
- `tests/Yello.Tests.Slices/Tasks/TaskEndpointTests.RemovedStatus.cs`
- `Yello.Application/Lists/ViewList/ViewListHandler.cs`
- `Yello.Application/Statuses/ProjectStatusDeltaDraft.cs`
- `Yello.Application/Statuses/ProjectStatusDeltaWriter.cs`
- `Yello.Application/Statuses/ProjectStatusSet.cs`
- `Yello.Application/Statuses/RemoveProjectStatus/RemoveProjectStatusHandler.cs`
- `Yello.Application/Statuses/RestoreProjectStatus/RestoreProjectStatusHandler.cs`
- `Yello.Application/Tasks/CreateTask/CreateTaskHandler.cs`
- `Yello.Application/Tasks/EditTaskAttributes/EditTaskAttributesHandler.cs`
- `Yello.Application/Tasks/MoveTask/MoveTaskHandler.cs`
- `Yello.Client/Components/BoardView.Pager.cs`
- `Yello.Client/Components/BoardView.Paging.cs`
- `Yello.Client/Components/StatusDeltaEditor.razor`
- `Yello.Client/Components/StatusDeltaEditor.Removal.cs`
- `Yello.Client/Components/StatusDeltaRow.razor`
- `Yello.Client/Components/StatusRemoveDialog.razor`
- `Yello.Client/Components/StatusRestoreItem.razor`
- `Yello.Client/Components/StatusRestoreList.razor`
- `Yello.Client/Localisation/ClientCopy.resx`
- `Yello.Client/Refusals/RefusalCopy.cs`
- `Yello.Client/wwwroot/css/components.css`
- `Yello.Contracts/ProblemTypes.cs`
- `Yello.Contracts/Projects/ProjectStatusRoutes.cs`
- `Yello.Contracts/Projects/RemoveProjectStatusRequest.cs`
- `Yello.Domain/Statuses/EffectiveStatusSet.cs`
- `Yello.Domain/Statuses/IProjectStatusStore.cs`
- `Yello.Domain/Statuses/ProjectStatusDelta.cs`
- `Yello.Domain/Statuses/StatusDeltaOperation.cs`
- `Yello.Domain/Tasks/ITaskStore.cs`
- `Yello.Host/Endpoints/ListEndpoints.cs`
- `Yello.Host/Endpoints/ProjectStatusAnswers.cs`
- `Yello.Host/Endpoints/ProjectStatusEndpoints.cs`
- `Yello.Host/Endpoints/ProjectStatusRemovalEndpoints.cs`
- `Yello.Host/Program.cs`
- `Yello.Infrastructure/Persistence/Configurations/StatusDeltaOperationConfiguration.cs`
- `Yello.Infrastructure/Persistence/Migrations/20261121190936_StatusDeltaSequenceUnique.cs`
- `Yello.Infrastructure/Persistence/Migrations/20261121190936_StatusDeltaSequenceUnique.Designer.cs`
- `Yello.Infrastructure/Persistence/Migrations/YelloDbContextModelSnapshot.cs`
- `Yello.Infrastructure/Persistence/ProjectStatusStore.cs`
- `Yello.Infrastructure/Persistence/TaskStore.cs`
- `_bmad-output/implementation-artifacts/6-2-remove-a-status-from-a-project-by-migrating-its-tasks.md`

## Change Log

- 2026-11-21: Story created (bmad-create-story). Status: ready-for-dev.
- 2026-11-21: Implemented (bmad-dev-story): `Remove` delta kind and re-anchoring; remove and add-back routes, slices and store statements in one transaction under the Project's lock; Task edit and move check the Status under that lock; unique delta `Sequence` index; Status-sort cursor on a removed Status refused; editor remove control, destination dialog, in-flight region and add-back list; pager selection cleared. Open questions Q1-Q12 decided at option (a). ATDD scaffolds activated (3 corrected), 11 isolation rows live, owned ledger rows dispositioned. Status: review.
- 2026-11-21: Code review (bmad-code-review): 11 patches applied. In the removal dialog, the pressed commit now stays until the progress region holds focus, and a press in flight is ignored. The focus trap wraps both ways. "Add back" focuses the restored row. The only Status goes straight to the server's never-empty refusal. AC7's add-back round trip asserts the invariant after every step, and AC9's case proves the Assignee survives. Six test comments that broke the comment rule were fixed. 10 low findings deferred to the ledger. Plants 4, 9, 10 and 11 were run and all caught. One full run including the browser suite: 2875 total, 2864 passed, 0 failed, 11 skipped (log `artifacts/test-runs/6-2-remove-a-status-from-a-project-by-migrating-its-tasks-review.log`). Status: done.
