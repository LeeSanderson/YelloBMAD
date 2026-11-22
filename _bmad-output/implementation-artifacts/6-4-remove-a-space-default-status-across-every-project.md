---
baseline_commit: 5b597a601ef70e6afd53eae4e77c98a3910a2d21
---

# Story 6.4: Remove a Space default Status across every Project

Status: review

Epic: 6 — Shape Statuses per Project and retire work safely (fourth of six stories)
Story key: `6-4-remove-a-space-default-status-across-every-project`

**Requirements owned.** Story Coverage Index (`epics.md:383`): **FR-27** (the removal half), **FR-24** (the default set is never empty; removing its last Status is refused), **AR-24** (`epics.md:212`: no endpoint accepts a removal without the mapping it requires), **UX-DR16** (the removal half of report-and-ask). Also carried:

- **FR-26** (`prd.md:487-490`): removal is always a migration. An unoccupied removal needs no mapping. The destination must be in the set in force **after** the removal.
- **FR-27 removal half** (`prd.md:500-506`):
  - one Space-wide destination, applied wherever a Project can accept it;
  - Projects that already removed the Status are unaffected;
  - a Project whose post-removal set lacks the destination is reported with its affected Task count and needs its own destination from that set;
  - nothing applies until every reported Project has one;
  - one transaction or nothing;
  - "An earlier draft let affected Tasks fall to the Project's first Status; rejected" (`:506`, `:878`).
- **AD-16 / AD-17 / AD-18** (`ARCHITECTURE-SPINE.md:186-206`, with the 6.1-6.3 notes at `:191-193`).
- **S-1** (`test-design-qa.md:318`, P0): of the six atomicity cases, this story owns "FR-27's per-Project exceptions". **S-2** (`:319`, P0): no Task holds a Status outside its Project's set. Assert S-2 after every case.
- **FR-16**: `Capability.ConfigureSpaceDefaultStatuses` (Owner, Admin), already declared by the three 6.3 writes.
- **NFR-1 / FR-15 / AD-2 / AD-3** (I-1 to I-4, A-4, P0): the new route carries `{spaceId}` and gets isolation rows on both surfaces.
- **NFR-8** (`prd.md:736-751`): 50 Projects per Space, 5,000 Tasks per Project (`BoundRegistry.cs:29-30`).
- **UX**:
  - `EXPERIENCE.md:355-364` (Space-level removal; "The interface must not decide anything here");
  - `:259` (migration in flight, Space settings);
  - `:260` (refused: "Nothing applied. State it, and state which Project blocked it");
  - DESIGN.md `:473`, `:480` (no default selection in a consequential picker);
  - mockup `ux-designs/ux-YelloBMAD-2026-08-18/mockups/project-settings.html` §3 (`:151-179`, the report-and-ask dialog) and §4 (`:182-197`, in flight and refused).

**Ledger entries owned (`deferred-work.md`). Close each one, or re-own it with a reason:**

| Line | Entry | Recommended disposition |
|---|---|---|
| `:1045` | The pager's stale-selection fix has no browser case (re-owned from 6.3 to 6.4) | **CLOSE** (AC14, Q12). A Space-level removal is the first operation that takes a column off a mounted Board. |
| `:1056` | Adding back a default the Space no longer has would delete the `Remove` and answer `200` with an unchanged set | **CLOSE** (AC9, Q9). The removal deletes every `Remove` op of the Status with its definition, so the add-back finds nothing to restore and answers `404`. Also harden the handler: the id must also be a current default. |
| `:1061` | A rename's decision applies to the Projects that conflict at commit (Q4 of 6.3, owner 6.4) | **Decide** (Q10). Recommended: keep the rename's at-commit rule and close the entry with the reason. The removal cannot follow that rule because it has no fallback (AC4), so it refuses at commit instead. |
| `:1067` | "{n} Projects kept their own names" counts the dialog's list, not the Projects conflicting at commit | **CLOSE** (AC13, Q11). The announcement no longer states a count the client cannot know. |
| `:1022` | A `Place` whose anchor leaves the set is ignored; "the ignore rule stays only as the fallback for an anchor that leaves some other way (a Space-level removal, 6.3/6.4)" | **UPDATE**. The Space-level removal re-anchors as well (AC7), so no live `Place` op is left anchored on a deleted Status. The ignore rule stays as a fallback only. |
| `:114` | The first default Status name may not appear in any comment (S1135) | **Keep open**: a standing trap for the rest of the epic (see Traps). |
| `:1030`, `:1031` | Name comparison vs collation; look-alike names | Leave open. Removal compares no names. Note that in the ledger. |
| `:1038`, `:1039` | 6.1 migration not safe for the old revision; `Down()` lossy | Leave open. 6.4 needs **no migration** (see "Schema"). Note that in the ledger. |
| `:1066` | The 6.3 report names a Project deleted mid-request with an empty name (owner 6.3) | Not owned. The removal takes `ProjectsPerSpace` (Q5), so its own report cannot meet that race. Note it beside the entry; do not close it, because the rename still can. |
| `:1072` | `DeltasAsync` reads every delta O(Projects x rows) under the Exclusive lock (owner epic 6) | Leave open. 6.4 reuses the read once per request. Note it. |

## What already exists — do not rebuild it

| Concern | Where it lives today | What 6.4 does with it |
|---|---|---|
| Default rows | `StatusDefinition` with `OwnerProjectId == null`. It is referenced, all `NO ACTION`, by:<br>- `Task (SpaceId, StatusId)` (`TaskConfiguration.cs:86-90`);<br>- `StatusDeltaOperation (SpaceId, StatusId)` and `(SpaceId, AfterStatusId)` (`StatusDeltaOperationConfiguration.cs:76-86`). | **The removal deletes the default's row last**, after every Task has left it and every op that names it (as `StatusId` or `AfterStatusId`) is gone. Statement order follows from the keys, as in `ProjectStore.DeleteAsync` (`ProjectStore.cs:134-153`). |
| The one derivation | `EffectiveStatusSet.Of`, `Check`, `Contains`, `Reanchored(current, placed, removedId)` (`EffectiveStatusSet.cs:153`), `Moved` | Every Project's before-set and after-set come from `Of`. Re-anchoring reuses `Reanchored`. **No second derivation and no third ordering algorithm.** |
| Space plan | `SpaceDefaultStatusPlan` (Domain): `Add`, `Rename`, `Place`, `CheckDefaults` (the never-empty guard for the default set, unit-tested by 6.3 and **reachable over HTTP now**), `Judged`; `SpaceDefaultStatusChange` (`Added`, `RenamedStatusId`, `Positions`, `RenameOperationsDeletedIn`, `Conflicts`, `ClashingProjects`); `SpaceDefaultStatusVerdict` | Add a `Remove` plan (Task 1). It is the riskiest part: it judges every Project's after-set. The file is 360 lines, so a sibling (`SpaceDefaultStatusRemoval.cs`) is likely needed to stay under S1200/S1541. |
| Space writer | `SpaceDefaultStatusWriter` (Application): lock → `ListAsync` → `DeltasAsync` → plan → write or refuse → re-read the defaults. `SpaceStatusWriteResult` carries `Conflicts` and `Projects` | The removal goes through it, or a sibling writer with the same shape (Q3). The removal must also take the Project locks and count occupants **under** them, which the 6.3 writer does not do. |
| Space store | `ISpaceDefaultStatusStore` / `SpaceDefaultStatusStore`: `LockAsync` (StatusDefaults Exclusive), `DeltasAsync` (two queries, grouped), `CountTasksByProjectAsync(statusId)` (one grouped statement), `ProjectNamesAsync`, `WriteAsync` | Add the removal's statements (Task 2). |
| Project removal machinery | `ProjectStatusStore.MigrateTasksAsync(projectId, from, to)`: one set-based `UPDATE` through `OPENJSON`, keys `PositionKey.After(last)` in the source column's order, no destination sibling re-keyed (`ProjectStatusStore.cs:128-168`). `DeleteOperationsAsync`. `ProjectStatusDeltaDraft.Remove` (re-anchors with `Reanchored`) | **Reuse the migration, do not copy it.** Lift its body into one internal Infrastructure helper (for example `TaskStatusMigration.MigrateAsync(dbContext, projectId, from, to, ct)`). Both stores call it. The 6.2 scale case must stay green. |
| Locks | `ApplicationLocks`: `StatusDefaults(spaceId)` (Exclusive/Shared), `ProjectsPerSpace(spaceId)` (Project creation's count, Project deletion, Space deletion), `ProjectTasks(spaceId, projectId)` (every Task placer, Project deletion, every Project delta write). The `StatusDefaults` remarks already say: "A Space-level removal … must take this lock Exclusive and then every affected Project's ProjectTasks lock in a fixed order, by Project id." | Lock order for the removal: **StatusDefaults (X) → ProjectsPerSpace → ProjectTasks for each affected Project, in a fixed id order** (Q5). Rewrite the remarks so they describe what is built. |
| Task writers | Edit and move take `ProjectTasks` **before** reading the set. Creation reads it after the bound count took the lock (6.2, ledger `:1020` closed) | **Unchanged.** A Task write queued behind the removal reads the set after it commits and is refused `400 invalid-task-status`, or lands in the new first Status. Prove one race (AC8). |
| Project add-back | `RestoreProjectStatusHandler`: `draft.HasRemoved(statusId)` → else `NotFound` | After a Space removal, no `Remove` op of the Status exists, so add-back is `404` (closes `:1056`). Harden it: also require the id to be a current default (one condition, no new read: the defaults are already read by the writer). |
| Routes | `SpaceRoutes.Statuses`, `Status`, `StatusPlace` (+ `…Of` helpers); `SpaceDefaultStatusEndpoints.cs` (178 lines); `ProjectStatusRemovalEndpoints.cs` (the 6.2 remove endpoint, `POST …/remove` with `RemoveProjectStatusRequest(Guid? DestinationStatusId)`) | Add `SpaceRoutes.StatusRemove` = `POST /api/v1/spaces/{spaceId}/statuses/{statusId}/remove` (Q1). Copy the 6.2 endpoint shape. Put it in a new `SpaceDefaultStatusRemovalEndpoints.cs` if the 6.3 file would pass the caps. |
| Problem types | `status-destination-required` (409), `invalid-status-destination` (400), `status-set-empty` (409), `status-rename-conflict` (409) | Reuse the first three. Add **one** type for the blocked Projects (Q2), for example `status-project-destination-required` (409). |
| Problem body | `ProblemResponse(Type, Errors, Capability, Bound, Limit, Spaces, Conflicts, Projects)`; `ProjectIdentityResponse(Id, Name)`; `StatusRenameConflictResponse` | Add **one** nullable, additive member for the occupancy report (Q2). Reuse `Projects` for the Projects a `status-set-empty` names. Amend the gate that pins `ProblemResponse`'s parameters deliberately, as 6.3 did. |
| Space settings | `SpaceDefaultStatusSection.razor` (88) + `.razor.cs` (**500 lines: at the cap**); `SpaceStatusRow.razor` (175; its remarks say "There is no remove"); `StatusRenameCascadeDialog.razor`; the 6.2 `StatusRemoveDialog.razor` (163: the no-default picker, in-flight region, focus trap both ways, "the pressed commit stays until the progress region holds focus") | Add a "Remove" per row, plus a new `SpaceStatusRemoveDialog.razor`. Put the section's removal flow in a new partial (`SpaceDefaultStatusSection.Removal.cs`). Do not grow the 500-line file. Copy the 6.2 dialog's patterns; do not re-derive them. |
| Project editor | `StatusDeltaEditor` re-reads the Space defaults with every set it renders (6.3 AC13) | **Unchanged.** After a Space removal, the "Add back" list loses the Status on the editor's next render. |
| Board / pager / List | Drawn from the server's per-Project answer. `BoardView.Pager.cs:53` `ForgetSelectionThatLeft`, called from `UseRereadBoardAsync` (`BoardView.Paging.cs:288-295`). A column page for a Status not in the set is `404`. A Status-sort List cursor for a Status not in the order is `400 invalid-list-cursor` (6.2 AC15) | They follow on the next read with no change. AC14's browser case finally exercises the pager's fix. |
| Test helpers | `SpaceDefaultStatusWire` (writes, the held Status-defaults lock, the S-1 trigger pair), `StatusRemovalWire`, `DeleteWire.HoldProjectRangeAsync`, `TaskWire.WaitUntilBlockedOnALockAsync`, `TaskWire.SeedTasksAsync` (one set-based seed), `WireDatabase.TasksOutsideTheirProjectsSetSql` (S-2, knows `Remove`), `WireDatabase.DeltaOperationCountInAsync` | Reuse. Add a removal call to `SpaceDefaultStatusWire`. |
| Fake API | `FakeYelloApi.SpaceDefaultStatuses.cs` (defaults, Project sets and Board columns derived from them), `FakeYelloApi.StatusRemoval.cs` | Add the Space removal, its `409`s, and the effect on the Project sets and Board columns. |
| Gates that go red by design | `RefusalBoundaryGateTests.SpaceDefaultStatuses.cs`:<br>- `The_Space_Status_routes_declare_their_capability_serve_both_surfaces_and_consult_no_bound` (exact route list);<br>- `No_route_or_request_under_the_Spaces_Statuses_offers_a_removal_a_reset_or_a_mode` (forbids "Remove" and "Destination");<br>- the `ProblemResponse` parameter gate;<br>- the role-matrix theory (only if it needs a representative per route). | Amend each deliberately (Task 5). **The "no removal" gate becomes the AR-24 gate for the Space:** the only route under the Space's Statuses that removes is `…/remove`, and its request carries the destination and the exceptions. No other Space Status request names a removal, a reset or a mode. Keep "Reset", "Inherit", "Override" and "Mode" forbidden everywhere. |

**What is genuinely new in 6.4:**

1. A Space-level removal plan that judges **every** Project:
   - holds the Status and can accept the destination;
   - holds it, cannot accept, and needs an exception;
   - holds only that Status (refused);
   - already removed it (unaffected).
2. One request that, under the Space lock, `ProjectsPerSpace` and every affected Project's lock, in one transaction:
   - migrates the Tasks of up to 50 Projects;
   - deletes every op naming the Status;
   - re-anchors every place;
   - deletes the default's row.
3. The occupancy report in a `409`, and the blocked-Project refusal that names who blocked it.
4. The Space settings "Remove" control and the report-and-ask dialog with per-Project pickers, no default selection anywhere.

**Explicitly not in this story:**

- Moving Tasks between Projects (6.5, 6.6) and the accent-bordered `bulk-move-bar` (UX-DR17, 6.6). The in-flight state here is the Space-settings "Status migration in flight" state (`EXPERIENCE.md:259`), as in 6.2. It is **not** accent-bordered and has **no cancel in flight** (Q8).
- Removing several defaults in one request.
- A "renamed in n Projects" or "removed in n Projects" indicator (ledger, owner UX).
- No migration, no new NuGet package, no new project. No commit, push, branch, Azure login or deploy. The orchestrator commits.

## Story

As an Owner or Admin,
I want to retire a Status across a whole Space and be asked about every Project that cannot take my chosen destination,
so that no Task is silently placed somewhere I did not choose.

## Acceptance Criteria

1. **One Space-wide destination** (FR-27, AR-24, FR-26). `POST …/statuses/{statusId}/remove` with `RemoveSpaceStatusRequest(Guid? DestinationStatusId, IReadOnlyList<SpaceStatusDestinationRequest>? Exceptions)`. Each exception is `{ ProjectId, DestinationStatusId }`. The Space-wide destination must be **another default of this Space**. Anything else is `400 invalid-status-destination`, byte-identical whatever it is:
   - the removed Status;
   - a Project-added Status;
   - another Space's Status;
   - a random id.

   **Occupancy is decided under the locks** (AC6):
   - When **no** Task in the Space holds the Status, no destination is needed and the removal succeeds directly (FR-26, Q4).
   - When any Task holds it and no destination is sent, the request is refused `409 status-destination-required`, and nothing is written. The body carries the occupancy report (AC2), so the client can open the dialog from it.

2. **The report names every affected Project, with its Task count** (FR-27, UX-DR16; `prd.md:503`; Q2). The report lists every Project of the request's Space that holds the Status in its effective set **and** has at least one Task in it. Each entry has:
   - its id and name;
   - how many of its Tasks hold the Status;
   - its **own post-removal effective set** (id and the Project's name for each Status, in Board order). This is what that Project's picker offers, and it may include Statuses the Project added.

   The client computes which Projects cannot accept a chosen destination from this report. The server re-judges at commit (AC4). Projects outside the Space never appear (AC12).

3. **A Project that cannot accept the destination needs its own destination** (FR-27; `prd.md:503-504`). A Project is **blocked** when:
   - it is in the occupancy report (it holds the Status and has Tasks in it), **and**
   - its post-removal set does not contain the Space-wide destination (it removed that default).

   Each blocked Project must have an exception whose destination is in **that Project's** post-removal set (a Space default it holds, or a Status it added). An exception's destination outside that set is `400 invalid-status-destination`, naming that Project in `Projects`. An exception for a Project that is not blocked is ignored (Q6). A malformed exception list (an unreadable body, or one Project twice) is `400 invalid-status-destination`.

4. **No fallback, no silent placement** (FR-27; `prd.md:503-504`). If any blocked Project has no exception, nothing applies: `409 status-project-destination-required` (Q2), naming **every** blocked Project without an exception, with its count and its post-removal set. The decision is judged **at commit, under the locks**. A Project that became blocked after the client's report (another Admin removed the destination there) is refused here, never placed. This is the "which Project blocked it" refusal (`EXPERIENCE.md:260`, mockup §4).

5. **Never empty, at either level** (FR-24, FR-25; Q7).
   - Removing the Space's **last** default is refused `409 status-set-empty` with no `Projects` (6.3's `CheckDefaults`, now reachable over HTTP).
   - Removing a default that is the **only** Status left in some Project's set is refused `409 status-set-empty`, naming those Projects in `Projects`. This holds whatever their occupancy. Nothing is written.
   - **This contradicts `prd.md:505` / `epics.md:2281-2283`** ("a valid destination exists in every affected Project, so the operation is always satisfiable"); see Contradictions C1. The Admin's way out is to add a Status to that Project first.
   - Order of decisions under the locks:
     1. not a default → `404`;
     2. last default → `409 status-set-empty`;
     3. a Project left empty → `409 status-set-empty` + `Projects`;
     4. Space-wide destination invalid → `400`;
     5. occupied and no destination → `409 status-destination-required` + report;
     6. an invalid exception → `400` + `Projects`;
     7. a blocked Project with no exception → `409 status-project-destination-required`;
     8. write.

6. **One transaction across every Project, serialised with every writer** (AD-17, FR-27; `prd.md:504`; Q5). Lock order: `StatusDefaults` **Exclusive** → `ProjectsPerSpace` → `ProjectTasks` of every Project whose set holds the Status, taken in **one fixed order** (sorted ids, through one `ApplicationLocks` helper that 6.5 will reuse). Every read the plan judges happens after the last lock is granted:
   - the defaults;
   - every delta;
   - the Project list;
   - the occupancy counts.

   So:
   - a Project created meanwhile waits (`ProjectsPerSpace`);
   - a Project delta write waits (Shared `StatusDefaults`);
   - a Task creation, edit or move in an affected Project waits (`ProjectTasks`).

   Nothing takes `StatusDefaults` after `ProjectsPerSpace` or `ProjectTasks`, and nothing takes `ProjectsPerSpace` after `ProjectTasks`. State this in the remarks.

7. **What the commit writes, in key order** (AD-16, AD-17; `ProjectStore.cs:134` precedent):
   1. For each affected, occupied Project: migrate its Tasks to the Space-wide destination or its exception, with the reused set-based migration. Each moved Task goes to the **end** of its destination column, in its source-column order. Its id, title, Assignee, Labels, due date and creation time are unchanged. No destination sibling is re-keyed (AD-15, 6.2 AC9).
   2. For each Project whose delta has a `Place` op anchored on the Status (`AfterStatusId`): replace its places with `EffectiveStatusSet.Reanchored(before, placed, statusId)`. The places get new `Sequence` values from the Project's `NextSequence`.
   3. Delete every op whose `StatusId` is the Status: `Rename` and `Place` of it in any Project, and the `Remove` ops of the Projects that had already removed it.
   4. Delete the default's `StatusDefinition` row.

   The remaining defaults' `Position`s are not rewritten (gaps are harmless; Q9). **Invariant, asserted by unit test for every Project:** `Of(defaultsAfter, deltaAfter)` equals `Of(defaults, delta)` with the Status taken out. The rest of every Project's order, and every name, are unchanged.

8. **Projects that already removed it are unaffected** (FR-27; `prd.md:501`). Their effective set, its order and its names are identical before and after. They appear in no report and no count. Their `Remove` op of the Status is deleted with the definition, which is bookkeeping and not a visible change. A Project that **renamed** the Status is affected like any other: identity, not name (AD-16). The report shows its own name for the Status's replacement choices, and its Tasks migrate.

9. **Add-back after a Space removal** (ledger `:1056`). Adding back the removed Status in a Project that had removed it answers the boundary `404` and writes nothing. `RestoreProjectStatusHandler` also requires the id to be a current default. The Project editor's "Add back" list no longer offers it (6.3 AC13's re-read).

10. **Atomic under a forced failure** (S-1, P0, AD-17; Q13). A test-only trigger on `StatusDefinition` throws on the delete of the removed id. That is after every Task migration and op delete, so it is the **last** statement. Under it:
    - the request fails `5xx`;
    - every Task in every Project still holds the Status at its old key;
    - every op, the definition and every Project's set are as before;
    - S-2 holds.

    Anchored on the same request succeeding once the trigger is dropped. Seed it with at least three Projects: one accepting the destination, one blocked with an exception, and one that had removed the Status.

11. **Role** (FR-16, UX-DR27, C-6). The route declares `ConfigureSpaceDefaultStatuses`. Member and Viewer get `403` with the pipeline's refusal record, on Session and Token. In the client, the "Remove" control and the dialog are **absent from the DOM** for Member and Viewer.

12. **Isolation** (NFR-1, FR-15, AD-2, AD-3; I-1 to I-4, A-4). Each of these answers the boundary's byte-identical `404 not-available`, writes nothing, and records what the boundary rules say:
    - a foreign Space;
    - another Space's Status id in the caller's own Space path;
    - a Project-added Status id in the path.

    In the body:
    - A foreign or random Space-wide destination is AC1's `400`, identical.
    - An exception naming a foreign or random Project id is ignored, exactly as an unblocked own Project is (Q6). It is never named back.
    - An exception destination that is another Space's Status is AC3's `400`, identical to a random id.

    The report and every refusal's `Projects` name only Projects of the request's Space. Assert it with a foreign Space whose Project holds a Status of the **same name**. Rows go in `IsolationCases` on both surfaces, including an `ISO-TOKEN-OWN-OTHER-SPACE-*` row, and are live before review (GATE-003). **Never remove a default of SA in the shared world** (Traps).

13. **The Space settings removal** (UX-DR16, UX-DR27, UX-DR31, UX-DR42, NFR-9; `EXPERIENCE.md:355-364`; mockup §3, §4).
    - **Remove control.** Each row of the Default Statuses section gets a "Remove" (Owner/Admin), named for its Status through `aria-describedby`. The row's remarks are updated.
    - **First press.** It sends the removal with no destination:
      - `200`: removed directly, announced ("Removed "{name}"."), and focus goes to the section heading;
      - `409 status-destination-required`: opens the dialog from the report;
      - `409 status-set-empty`: shows its reason inline, naming the Projects where there are any.
    - **The dialog** (mockup §3):
      - Heading: "Removing "{name}" moves {n} Tasks." with singular and plural.
      - Line: "Choose where Tasks in "{name}" go. This applies everywhere it can."
      - The Space-wide picker over the remaining defaults, with **no default selection** (a "Choose…" placeholder).
      - Once a destination is chosen, the blocked Projects are listed: "{k} Projects don't have "{destination}" after this change. Each needs its own destination:". The table has columns Project, Tasks affected, and Destination. Each row's picker is over that Project's post-removal set, also with **no default selection**.
      - Commit: "Remove and move {n} Tasks". Cancel.
      - **Neither picker is pre-filled and no choice is ever made for the Admin** (`EXPERIENCE.md:364`).
    - **Commit with something unchosen** (Q14): the commit stays enabled, as every form here does. Pressed with a picker unset, it sends nothing, marks each unset picker `aria-invalid` with its message, and moves focus to the first. A gate line ("{m} destinations still needed") is shown as text beside the commit, as in the mockup.
    - **In flight:**
      - The commit is replaced by a progress region, `role="status"`, `tabindex="-1"`: "Moving {n} Tasks across {p} Projects." Focus moves to it.
      - The pressed commit stays until the region holds focus (6.2's fix).
      - No percentage and no bar. Commit, Cancel and Escape are ignored while in flight. There is **no cancel in flight** (Q8).
    - **Done:** a polite announcement ("Removed "{name}". {n} Tasks moved."), with the dialog closed and focus on the section heading (the row is gone).
    - **Refused:**
      - "Nothing was moved." plus the reason, in the dialog's error region.
      - For `status-project-destination-required`, the dialog **re-renders its blocked table from the refusal** (new rows unset), and the line names them ("{Project} no longer has "{destination}". Choose a destination for it.").
      - For `400` with `Projects`, it names that Project.
      - A `404` re-reads the defaults. The re-read runs inside the busy guard; do not repeat ledger `:1069`.
    - The focus trap wraps both ways. Escape and Cancel close it before commit and return focus to the row's Remove control.
    - **Rename announcement** (ledger `:1067`, Q11): the "keep" answer no longer states a count. Use "Renamed "{old}" to "{new}". Projects that renamed it kept their own names."
    - Copy in `ClientCopy.resx`, sentence case, singular and plural for every count. Logical properties, usable at 320px. Purged on Space switch, sign-out and Account switch.

14. **Boards and other readers after a Space removal** (ledger `:1045`).
    - Every Project's Board loses the column on its next read.
    - A column page for the removed Status is `404`.
    - A Status-sort List cursor naming it is `400 invalid-list-cursor`.
    - Task creation lands in the Project's first Status after the removal.
    - **Browser case** (fake API, below 768px), in `SmallViewportTests`: the pager has the column selected; the fake removes the Status at Space level; a deterministic Board re-read without remount (the path 6.3's Q11 identified: a Task send the fake refuses sets `_rereadBoard`, so `UseRereadBoardAsync` runs `ForgetSelectionThatLeft`) leaves no selection that names a column that is gone. If no deterministic path exists after looking, re-own the entry with that evidence (Q12).

15. **Idempotent, and the answer is the truth** (AD-18). The route goes through the pipeline's `Idempotency-Key`. A replay returns the original response and migrates nothing again. Success answers `200` with the Space's whole default list as `StatusResponse` rows, in order (as the other three Space writes).

16. **Scale** (NFR-8, AD-17; Q15). A removal over the bound envelope commits as one transaction and leaves S-2 holding: 50 Projects, the occupied ones holding the Status with up to 5,000 Tasks each, some blocked with exceptions. It is a tiered case (`[Trait("Tier", "Nightly")]`, `[Fact(Explicit = true)]`, alone in its file, seeded with `TaskWire.SeedTasksAsync`). The duration is written to the output and not asserted. The dev chooses the exact seed size: the full 250,000 if it fits the 2GB engine, otherwise the largest that does, recorded in the Debug Log. A PR-stage case covers correctness with a few Tasks per Project.

17. **S-2 after every case; derived, never materialised** (AD-16, AD-17, AR-23). Assert `WireDatabase.TasksOutsideTheirProjectsSetSql` = 0 after each Slices case. The schema gate (`ProjectEndpointTests.StatusSchema.cs`) stays green unamended. No row is written per Project except the re-anchored places.

## Tasks / Subtasks

- [x] **Task 1 — Domain: the removal plan** (AC1-AC5, AC7, AC8)
  - [x] `SpaceDefaultStatusRemoval` (name is the dev's), next to `SpaceDefaultStatusPlan`. It is a pure function of:
    - the defaults;
    - every delta, keyed by Project;
    - the Project ids of the Space (a Project with no delta holds every default);
    - the occupancy counts per Project;
    - the removed id, the Space-wide destination and the exceptions.

    It returns the verdict plus:
    - the occupancy report (each Project's post-removal set);
    - the blocked Projects missing an exception;
    - the Projects left empty;
    - the invalid-exception Project;
    - the plan: per Project, the destination, the re-anchored places to write, and the ops to delete.
  - [x] Every before-set and after-set comes from `EffectiveStatusSet.Of`. The after-set is `Of(defaults without the Status, delta without ops naming it, with re-anchored places)`. Re-anchoring is `EffectiveStatusSet.Reanchored`.
  - [x] `CheckDefaults` for the last default. `Check(after, null) == SetEmpty` for each Project.
  - [x] New `SpaceDefaultStatusVerdict` members as needed (`DestinationRequired`, `DestinationInvalid`, `ProjectDestinationRequired`; `SetEmpty` with Projects).
  - [x] Unit tests (no database) in `tests/Yello.Tests.Slices/Statuses/SpaceDefaultStatusRemovalTests.cs`:
    - plain Projects take the destination;
    - a Project that removed the destination is blocked, and with an exception it passes;
    - an exception that is a Project-added Status passes;
    - an exception outside the set is invalid;
    - a Project that removed the Status is unaffected;
    - a renamed Status is affected;
    - a Project holding only the Status → `SetEmpty` naming it;
    - the last default → `SetEmpty`;
    - unoccupied → no destination needed;
    - a blocked Project with zero Tasks is not reported (Q4);
    - **the order invariant of AC7 for a Project whose places anchor on the Status, on both sides of it**.
- [x] **Task 2 — Infrastructure** (AC6, AC7, AC10)
  - [x] `ApplicationLocks`:
    - a helper that takes `ProjectTasks` for a set of Projects in one fixed sorted order (one `sp_getapplock` per Project, or one batch);
    - rewrite the `StatusDefaults` remarks to state the built order: StatusDefaults → ProjectsPerSpace → ProjectTasks (sorted);
    - name 6.5's two-Project move as a future user of the same helper.
  - [x] Lift `ProjectStatusStore.MigrateTasksAsync`'s body into a shared internal helper. Both stores call it, with no behaviour change. Run the 6.2 removal cases filtered.
  - [x] `ISpaceDefaultStatusStore` / `SpaceDefaultStatusStore`:
    - `LockForRemovalAsync` (or extend `LockAsync`): `ProjectsPerSpace`, then the Project list, then the affected Projects' `ProjectTasks`;
    - the Project ids and names of the Space (one query);
    - occupancy counts (the existing `CountTasksByProjectAsync`, called after the locks);
    - `RemoveAsync(plan)`, issuing the statements in AC7's order: migrate per Project, delete and re-insert the re-anchored places, delete ops by `StatusId`, delete the definition.

    No `Where SpaceId`. Raw SQL only in Infrastructure, with parameters.
- [x] **Task 3 — Application** (AC1-AC6, AC15)
  - [x] `Yello.Application/Statuses/RemoveSpaceStatus/RemoveSpaceStatusHandler.cs`. It goes through `SpaceDefaultStatusWriter` (extended) or a sibling writer (Q3), with the decision order of AC5. Reports carry Project names read under the locks.
  - [x] `SpaceStatusWriteOutcome` gains the new outcomes, and `SpaceStatusWriteResult` gains the report.
  - [x] `RestoreProjectStatusHandler`: also require the id to be a current default (AC9).
  - [x] Handler tests with fakes.
- [x] **Task 4 — Contracts and Host** (AC1-AC5, AC11, AC12, AC15)
  - [x] `SpaceRoutes.StatusRemove` + `StatusRemoveOf`. Its remarks list every answer.
  - [x] `Yello.Contracts.Spaces.RemoveSpaceStatusRequest(Guid? DestinationStatusId, IReadOnlyList<SpaceStatusDestinationRequest>? Exceptions)` and `SpaceStatusDestinationRequest(Guid ProjectId, Guid DestinationStatusId)`.
  - [x] `ProblemTypes.StatusProjectDestinationRequired` (409; the name is the dev's).
  - [x] `ProblemResponse` gains one additive, nullable member for the report, for example `Removal` → a list of `SpaceStatusOccupancyResponse(ProjectId, ProjectName, TaskCount, IReadOnlyList<StatusChoiceResponse> Destinations)`, emitted only on `status-destination-required` (Space) and `status-project-destination-required`. `Projects` is reused for `status-set-empty` and the invalid exception.
  - [x] Endpoint: `RequiresCapability(ConfigureSpaceDefaultStatuses)`, both surfaces, idempotent, `200` with the defaults. An unreadable body is `400 invalid-status-destination`. Register it in `Program.cs`.
  - [x] Amend the gates listed in "What already exists" and add the Space AR-24 gate: the remove request has a destination and an exceptions member, and no other Space Status request or route can remove.
- [x] **Task 5 — Client** (AC11, AC13, AC14)
  - [x] `SpaceStatusRow`: a "Remove" button, raised to the section.
  - [x] `SpaceDefaultStatusSection.Removal.cs` (partial) for the flow.
  - [x] `SpaceStatusRemoveDialog.razor` per AC13, reusing the patterns of `StatusRemoveDialog`. It may need a child `SpaceStatusExceptionRow.razor` for the caps.
  - [x] `RefusalCopy` maps the new type and the `Projects` variants.
  - [x] `ClientCopy.resx`: the new copy and the rename "keep" announcement (ledger `:1067`).
  - [x] `FakeYelloApi`: the Space removal with each `409`/`400`, and the Project sets and Board columns follow.
  - [x] `SmallViewportTests`: the pager case (AC14).
- [x] **Task 6 — Ledger and docs**
  - [x] Write the disposition of every owned ledger entry in `deferred-work.md`. Leave none blank.
  - [x] Add an AD-16/AD-17 additive dated note:
    - a Space-level removal deletes the default's row after migrating every affected Project's Tasks and deleting every op naming it;
    - it re-anchors dependent places;
    - its lock order is as in AC6.

    Additive only.
  - [x] Add a low ledger entry for Lee about the transaction size vs NFR-5 and the 240 s ingress timeout (Q15), with the nightly measurement.
- [x] **Task 7 — Tests** (see Testing requirements)

## Dev Notes

### The shape of the story in one paragraph

6.2 made a Project-level removal: one Project lock, one migration, re-anchored places, a `Remove` op or a deleted definition. 6.3 made Space-level writes under a Space lock that Project delta writes take Shared, and judged every Project's after-set with the one derivation. 6.4 combines them. One request under the Space lock, `ProjectsPerSpace` and every affected Project's Tasks lock:
- judges every Project against one Space-wide destination;
- refuses until every blocked Project has its own destination;
- then, in one transaction, migrates each Project's Tasks, removes every trace of the Status from every delta, and deletes the default's row.

The only per-Project rows written are the re-anchored places. Everything else is derived.

### Dominant finding: "always satisfiable" is false, and the lock set is the real risk

- **C1: a Project can hold the removed default as its only Status.** 6.1 refuses only an empty set, so a Project that removed every other default and added none holds {S}. Removing S at Space level would empty it. No destination exists there, so the operation is **not** always satisfiable, contrary to `prd.md:505` and `epics.md:2281-2283`. Recommended (Q7): refuse `409 status-set-empty`, naming those Projects. It is the "which Project blocked it" refusal, and the Admin adds a Status there first. This keeps FR-25 ("a Project's effective set can never be empty") intact, which the PRD's sentence is derived from.
- **The second risk is the lock set.** A Task writer takes only its Project's `ProjectTasks` lock. Unless the removal holds that lock for **every** Project whose set holds S, a Task can be put into S after the occupancy count. The definition delete then fails `547` (a `500`, not corruption, but a removal that cannot finish). A Project created after the Project list was read is the same hole, which is why `ProjectsPerSpace` is taken first.
- **No migration.** Every row 6.4 writes or deletes exists today.

### Contradictions found (recorded; do not resolve beyond what is stated)

- **C1.** "Always satisfiable" (`prd.md:505`, `epics.md:2281-2283`, `EXPERIENCE.md:362`) is false for a Project whose only Status is the one removed. See Q7.
- **C2.** The epic asks for "one Space-wide destination" unconditionally (`epics.md:2257`, `EXPERIENCE.md:357`). FR-26 (`prd.md:489`) says an unoccupied removal needs no mapping. See Q4.
- **C3.** `prd.md:503` reports a Project whose set lacks the destination "and how many of its Tasks are affected", which can be zero. A Project with no affected Task has nothing to place. See Q4.
- **C4.** `EXPERIENCE.md:259` gives Space-level migration the "same treatment" as the bulk move, which has the only cancel. UX-DR16 and mockup §4 show no cancel in flight, and DESIGN.md `:474` reserves the bulk-move bar as the only accent-bordered transient. See Q8.
- **C5.** The mockup's commit is **disabled** with "2 destinations still needed". Every form here keeps its commit enabled and explains on press (6.2's `StatusRemoveDialog` remarks). See Q14.
- **C6.** One transaction over 50 × 5,000 Tasks on a 0.5 vCore, `MAXDOP = 1` database (`ARCHITECTURE-SPINE.md:85`, `:333`) sits uneasily with NFR-5's 500 ms write p95 (`prd.md:718`) and the 240 s ingress timeout (`ARCHITECTURE-SPINE.md:312`). No source grants an exemption. See Q15.
- **C7.** The mockup lives in `project-settings.html` but its chrome reads "Space settings". The removal is a Space-level act, so it lives in Space settings (as 6.3's C3).
- **C8.** The task brief points at `sprint-change-proposal-2026-09-13.md` for the "rename asked, removal guessed" defect. That proposal is about FR-1 registration. The defect is recorded in `prd.md:37`, `:506`, `:878` and `EXPERIENCE.md:364`.

### Open questions

Each has a recommendation, which is the default. The dev phase decides each as a reversible, recorded decision, in Completion Notes and the Change Log. **None is marked [LEE]** for these reasons:
- no spine rule changes (AD-16/AD-17 get additive notes);
- there is no schema change;
- no RLS rule changes;
- the contract changes are additive.

Q7 corrects a PRD sentence by applying the PRD's own FR-25, and Q15 records a measurement for Lee. Neither blocks.

1. **Route** (AC1). **(a) Recommended:** `POST …/statuses/{statusId}/remove` with `{ destinationStatusId?, exceptions?: [{ projectId, destinationStatusId }] }` → `200` with the defaults. The Project shape from 6.2, with the per-Project destination map the readiness report expects (`:243-247`, `:548`). (b) `DELETE` with a body. Cost: bodies on `DELETE` are poorly supported, and the 6.3 gate forbids `DELETE` under the collection.
2. **How the report reaches the client** (AC2, AC4). **(a) Recommended:** the removal itself reports. The first press sends no destination:
   - `200` when nothing is occupied;
   - otherwise `409 status-destination-required` carrying the occupancy report (each Project's count and post-removal set).

   The client computes blocked Projects locally as the Admin picks. The commit is re-judged under the locks, and `409 status-project-destination-required` names every blocked Project still without a destination. This follows 6.2's direct removal → `409` → dialog and 6.3's "a refusal that must name things carries them". (b) A `GET …/statuses/{statusId}/removal` preview read. Cost: a new route with its own isolation rows and gates, and a second source of the same facts.
3. **Writer** (AC5, AC6). **(a) Recommended:** a sibling `SpaceDefaultStatusRemovalWriter` that reuses the store and the reads but owns the multi-lock and the occupancy count. The 6.3 writer stays as it is. (b) Extend `SpaceDefaultStatusWriter`. Cost: its `Func<defaults, deltas, change>` plan shape has no room for occupancy read under extra locks.
4. **Unoccupied, and zero-Task Projects** (C2, C3). **(a) Recommended:**
   - no destination is needed when no Task in the Space holds the Status;
   - a destination sent anyway is validated;
   - a Project with no Task in it is never reported and needs no exception.

   This is FR-26's rule, applied per Project: there is nothing to place, so nothing is decided for the Admin. (b) Always require a destination, and report zero-Task Projects. Cost: asks the Admin about Projects where no Task moves.
5. **Locks** (AC6). **(a) Recommended:** StatusDefaults X → ProjectsPerSpace → ProjectTasks for each Project whose set holds the Status, sorted. (b) ProjectTasks for every Project in the Space. Cost: blocks Task writes in unaffected Projects for the duration, for no correctness gain.
6. **Exceptions for unblocked or unknown Projects** (AC3, AC12). **(a) Recommended:** ignored. The Space-wide destination is the Admin's own choice for every Project that can take it, and ignoring a foreign id is byte-identical to ignoring a random one. (b) Refuse `400`. Cost: a stale dialog whose Project became unblocked is refused for no reason.
7. **A Project left empty** (C1). **(a) Recommended:** refuse `409 status-set-empty` naming the Projects. (b) Let the exception name… nothing: impossible, since the set has no other Status. (c) Add a Status to that Project automatically. Cost: a decision made for the Admin, which FR-27 forbids.
8. **Cancel in flight** (C4). **(a) Recommended:** Cancel and Escape before commit only. In flight there is nothing to cancel: the request is atomic and already sent, as in 6.2. Not accent-bordered. (b) A cancel that aborts the HTTP request. Cost: the server may still commit, and the "cancelled" message would then be false.
9. **Definition delete and positions** (AC7, AC9). **(a) Recommended:** delete the default's row, together with every `Remove`, `Rename` and `Place` op of it. Leave the other defaults' `Position` gaps. Add-back then finds nothing (closes `:1056`). (b) Keep a tombstone row. Cost: every reader must learn to skip it, which is a second derivation. (c) Compact positions. Cost: writes for nothing visible.
10. **Ledger `:1061`, the rename's at-commit rule**. **(a) Recommended:** keep the rename's at-commit rule and close the entry with the reason. The rename has a safe outcome either way, because the decision is the Admin's single policy (`prd.md:503`). The removal refuses at commit because a placement the Admin never saw would be a silent one. (b) Make the rename echo the Project ids it was shown. Cost: a contract change and another round trip for a race between two Admins.
11. **Ledger `:1067`, the kept count**. **(a) Recommended:** reword the "keep" announcement without a count (AC13). (b) Return the count in the rename's answer. Cost: changes the shape of a `StatusResponse`-list answer.
12. **Ledger `:1045`, the pager case**. **(a) Recommended:** write it through the fake (AC14) and close the entry. (b) If, after looking, no deterministic re-read path exists without remount, re-own the entry to **UX** with that evidence.
13. **S-1 placement** (AC10). **(a) Recommended:** the trigger on the definition delete, which is the last statement, so every migration and op delete is rolled back. (b) A trigger on `Task`. Cost: it fails before the op deletes and proves less.
14. **The commit with unset pickers** (C5). **(a) Recommended:** the commit stays enabled. Pressing it sends nothing, marks every unset picker and focuses the first. The mockup's "{m} destinations still needed" is shown as text. (b) Disable it as the mockup draws it. Cost: breaks the project-wide rule, and a disabled button explains nothing to a screen reader.
15. **Scale** (C6, AC16). **(a) Recommended:**
    - a nightly Explicit case that records the duration;
    - a low ledger entry for **Lee** stating the measured duration against NFR-5 and the 240 s ingress timeout;
    - no command timeout change unless the case exceeds the 30 s default per statement (then raise it on this request only, with the measurement).

    (b) Chunk the migration across transactions. Cost: breaks FR-27's "one transaction or not at all".

### Locking (RCSI)

- **Order:** StatusDefaults (X) → ProjectsPerSpace → ProjectTasks (sorted ids) → Task rows → Labels.
  - Project delta writes: StatusDefaults (S) → ProjectTasks.
  - Project deletion: ProjectsPerSpace → ProjectTasks.
  - Project creation: ProjectsPerSpace.
  - Task writers: ProjectTasks.

  No cycle.
- A Labels-only edit takes a Task row without the Project lock. The migration waits for that row, and the edit never waits on a Project lock (6.2's remark).
- Every read the plan judges happens **after** the last lock is granted. Plain statements under RCSI then see every committed write of every holder.
- Race cases (deterministic, `LCK_M_%`, never timing):
  1. Hold a Project's Tasks lock (a Task edit into S in flight); the Space removal queues. Released, the edited Task is migrated. Without a destination the refusal is `409` with the count that includes it.
  2. Hold the Space removal (its StatusDefaults); a Task edit into S queues. Released: `400 invalid-task-status`.
  3. Hold the removal; a Project restore of the destination default (Shared StatusDefaults) queues. Released: it applies to the post-removal state.
  4. The reverse: a Project removes the destination default first. The Space removal then sees that Project blocked and refuses `409 status-project-destination-required` naming it. This is the mockup's "no longer has the Status you chose".
  5. Hold `ProjectsPerSpace` via the removal; a Project creation queues and then holds a set without S.

### Architecture compliance (must)

- **AD-21 ring:**
  - Domain: plan and port in `Yello.Domain/Statuses/`.
  - Application: `Yello.Application/Statuses/RemoveSpaceStatus/`.
  - Infrastructure: store, locks and migration helper in `Persistence/`.
  - Host: `Yello.Host/Endpoints/`.
  - Contracts: `Yello.Contracts/Spaces/` (the requests must be in the Spaces namespace, as the 6.3 gate expects).
- **AD-1 / AR-3:** no Role read in a slice.
- **AD-2:** no `Where SpaceId` and no `IgnoreQueryFilters`. The 6.3 review gate `No_production_code_disarms_the_query_filter_outside_its_allowlist` enforces it. Lock names carry `ScopedSpaceId`.
- **AD-3 / AD-20:** an in-Space not-found answers the boundary body and records nothing.
- **AD-16:** one derivation; ids never reused; nothing materialised.
- **AD-17:** one transaction; S-2 after every case; no endpoint removes without its mapping.
- **AD-18:** idempotent.
- **AD-19 / C-1, C-2:** additive contracts only. `StatusResponse` and `ProjectStatusResponse` are unchanged.
- **AR-34:** stable `urn:yello:problem:*` codes.
- **Comments** (CLAUDE.md): explain why; no story, AC, Q or ledger ids, dates or history.

### Traps

- **S1135 (ledger `:114`).** Never write the first default's name in any comment. In code use `DefaultStatusSet.Names[0]`.
- **Shared isolation worlds.** `ISO-DIRECT-STATUSES`, `ISO-AGGREGATE` and `ISO-RECORD-KIND` assert SA's three Status ids exactly. A removal row must run in a Space of its own (6.3's practice: a fresh Account's Space, seeded once per world). Do not push B past its 50-Project bound.
- **Key order of deletes.** Tasks, then ops by `AfterStatusId` (replaced), then ops by `StatusId`, then the definition. A `547` in a test means a statement is out of order or a Project lock is missing. Never "fix" it with a cascade.
- **Re-anchored places need new `Sequence`s.** `(SpaceId, ProjectId, Sequence)` is unique (6.2's migration). Delete the old places first.
- **`Place` anchored on S in a Project that had removed S.** 6.2 re-anchors at removal time, so none should exist. Plan for it anyway: it would block the definition delete.
- **Caps.** `SpaceDefaultStatusSection.razor.cs` is at 500 lines and `SpaceDefaultStatusPlan.cs` is at 360. Use new partials and components from the start.
- **The 6.3 forbidden-words gate** forbids "Remove" and "Destination" under the Space's Statuses. It must be rewritten, not deleted, so it still forbids a reset or a mode.
- **Fake vs host.** The fake must compute blocked Projects and the empty-set refusal the way the host does, or the browser cases prove the fake.
- **Plants** must compile under the analyzers. Restore each from saved content in a `finally`. Never use `git checkout --`, `git restore` or `git reset`. Run them in small groups and check `git status` for residue.
- `TreatWarningsAsErrors`: 0 warnings is a gate. `--no-build` runs stale binaries.

### Library / framework

No new package or project, and no web research was needed. Everything used is already in the codebase:
- EF Core 10.0.11 (`ExecuteDeleteAsync`, `ExecuteSqlAsync`, `SqlQuery`);
- SQL Server 2025 (RCSI, `sp_getapplock`, `OPENJSON`);
- ASP.NET Core 10.0.11 Minimal APIs with Asp.Versioning.Http 10.0.0;
- Blazor WebAssembly 10.0.11 with Localization;
- xunit.v3 4.0.0, Testcontainers 4.6.0, Playwright 1.62.0, ArchUnitNET 0.13.3.

### Testing requirements

Follow `tests/TESTING-CONVENTIONS.md`:
- traits `:44-86`;
- rules and races `:87-207` (`:102-191`);
- browser cases `:209-437`;
- the shared container `:621-690`;
- isolation `:790-912`;
- tiers `:946-973`.

- **Traits.**
  - P0: isolation, race, S-1 and S-2 cases.
  - `Requirement` values: FR-27, FR-24, FR-26, AR-24, AD-16, AD-17, AD-18, FR-16, NFR-1, NFR-8, UX-DR16.
- **No new container-backed class.** Slices cases go in new partials of `ProjectEndpointTests`:
  - `.SpaceDefaultRemoval.cs`;
  - `.SpaceDefaultRemovalRace.cs`;
  - `.SpaceDefaultRemovalScale.cs` (tiered, alone).

  Isolation rows go in `IsolationCases.SpaceDefaultStatusRemoval.cs`. The browser class is `SpaceDefaultStatusRemovalTests.cs` (fake API). The pager case extends `SmallViewportTests`.
- **Seed through the product.** Raw SQL only for catalogue reads, S-2, op counts, the S-1 trigger, holding locks and the scale seed.
- **Memory budget.** One `dotnet test` at a time. Iterate filtered. Develop ends with one background full run, `--max-parallel-test-modules 1 -p:YelloSkipBrowserSuite=true`.

**Minimum Slices cases:**

| # | Case |
|---|---|
| 1 | Unoccupied default removed with no destination: `200`. The defaults lose it, every Project's set loses it, and no op of it remains. |
| 2 | Occupied, no destination → `409 status-destination-required` with the report (each occupied Project, its count and post-removal set, including a Project-added Status where it has one). Nothing written. |
| 3 | Destination accepted everywhere: Tasks in every Project move to the end of the destination column in source order; fields unchanged; S-2. |
| 4 | A blocked Project with no exception → `409 status-project-destination-required` naming it with its count; nothing written anywhere. |
| 5 | Same with an exception (a default it holds, and a Status it added): `200`. Its Tasks go there and the others take the Space-wide destination. |
| 6 | An invalid exception destination → `400` naming that Project. A foreign Status as exception → the same `400`. Exceptions for an unblocked, foreign or random Project are ignored. |
| 7 | Invalid Space-wide destination (the Status itself, Project-added, foreign, random) → identical `400`. |
| 8 | Last default → `409 status-set-empty` with no Projects. A Project whose only Status is it → `409 status-set-empty` naming it. Both write nothing. |
| 9 | A Project that removed the Status: its set, order and names are unchanged and it is not reported. A Project that renamed it: affected, and migrated. |
| 10 | A Project whose places anchor on the Status: the remaining order is unchanged. |
| 11 | Add-back of the removed Status in a Project that had removed it → `404`. |
| 12 | Board column page `404`; List cursor `400`; Task creation lands in the new first Status. |
| 13 | Member and Viewer `403` on both surfaces; a role-matrix row if the theory needs one. |
| 14 | Foreign Space, foreign Status in the path, Project-added Status in the path → `404`, byte-identical. A foreign same-named Project is never reported. |
| 15 | Idempotent replay: the original answer, nothing migrated again. |
| 16 | S-1 (AC10). |
| 17 | Races 1-5 (Locking). |
| 18 | S-2 after every case; the schema gate is unchanged. |

**Minimum plant table** (restore each in a `finally`):

| # | Plant | Expected red |
|---|---|---|
| 1 | A blocked Project without an exception falls to its first Status | case 4 |
| 2 | Projects that removed the Status are reported | case 9 |
| 3 | The removal skips the Project Tasks locks | race 1 (and the `547` it causes) |
| 4 | It skips `ProjectsPerSpace` | race 5 |
| 5 | No re-anchoring | case 10 |
| 6 | The Project-left-empty check is skipped | case 8 |
| 7 | The dialog's Space-wide picker pre-selects the first destination | the browser no-default case |
| 8 | An exception picker pre-selects its first Status | the browser no-default case |
| 9 | The client predicate admits Member | the matrix gate and the browser absence case |
| 10 | Add-back skips the current-default check and the ops are not deleted | case 11 |

**Browser cases (fake API):**
- The Remove control is absent for Member and Viewer.
- An unoccupied removal is direct and announced.
- An occupied removal opens the dialog with the scope and no selection.
- Choosing a destination lists the blocked Projects with counts and unset pickers.
- A commit with an unset picker sends nothing and focuses it.
- The full commit shows the in-flight region with focus and no percentage, then announces Done.
- A refusal at commit re-renders the blocked table, naming the Project.
- `status-set-empty` with Projects is shown inline.
- The trap wraps both ways; Cancel and Escape send nothing and return focus.
- Usable at 320px.
- The pager case (AC14).
- The reworded "kept their own names" announcement.

### Previous story intelligence (6.1-6.3)

- **The shape that works:** lock → read under the lock → plan purely → judge every after-set with `Of` + `Check` → write in key order → answer the truth. 6.3's plan judged every Project. 6.2's writer migrated in one `UPDATE` and re-anchored.
- **6.3 Debug Log:** the conflict report counted Tasks of `Guid.Empty` because the refused plan did not carry the Status id. Make every refusal carry what its report needs.
- **6.3 review:** a gate now forbids `IgnoreQueryFilters` in production. RLS alone hid a bypass from every wire case.
- **6.3's browser amendments:** the new rows add one "Rename" per default. A new "Remove" per row will likewise collide with any locator that reads "Remove" across `main` (for example Labels or Members sections). Scope locators.
- **6.2's client lessons:**
  - the pressed commit stays until the progress region holds focus;
  - the trap wraps both ways;
  - a control that removes itself says where focus goes;
  - singular and plural copy;
  - a refusal header must not claim "nothing moved" when the request never got an answer (ledger, UX).
- **Scaffolds corrected before:**
  - `STRING_AGG` needs `COLLATE DATABASE_DEFAULT`;
  - a record with a list member compares by reference (compare a projection);
  - a wait on `.board-column` `Nth(n)` matches the loading skeleton (wait on a heading);
  - every Token-reachable route needs an `ISO-TOKEN-OWN-OTHER-SPACE-*` row.

### Git intelligence

- 6.3: `70e8716` story, `4b1ed3d` ATDD, `86eec80` implementation, `ab19a56` review close. 6.2: `0c017f7`, `e9c39fb`, `e4c7cc6`, `23caa85`.
- The cycle per story: story file → ATDD scaffolds (`Skip = "red: …"`, `atdd-checklist-<key>.md`, `PendingUntil` isolation rows) → implementation → review. The orchestrator commits each phase.
- The latest migration is `20261121190936_StatusDeltaSequenceUnique`. 6.4 expects none.

### Project Structure Notes

- **New (expected):**
  - Domain: `Yello.Domain/Statuses/SpaceDefaultStatusRemoval.cs`.
  - Application: `Yello.Application/Statuses/RemoveSpaceStatus/RemoveSpaceStatusHandler.cs`, possibly `SpaceDefaultStatusRemovalWriter.cs`.
  - Infrastructure: `Yello.Infrastructure/Persistence/TaskStatusMigration.cs`.
  - Contracts: `Yello.Contracts/Spaces/RemoveSpaceStatusRequest.cs`, `SpaceStatusDestinationRequest.cs`, the occupancy response records.
  - Host: `Yello.Host/Endpoints/SpaceDefaultStatusRemovalEndpoints.cs`.
  - Client: `SpaceDefaultStatusSection.Removal.cs`, `SpaceStatusRemoveDialog.razor`, possibly `SpaceStatusExceptionRow.razor`.
  - Tests: as listed above, plus `RefusalBoundaryGateTests.SpaceDefaultStatusRemoval.cs` and `FakeYelloApi.SpaceDefaultStatusRemoval.cs`.
- **Updated (expected):**
  - `ISpaceDefaultStatusStore.cs`, `SpaceDefaultStatusStore.cs`, `ApplicationLocks.cs`, `ProjectStatusStore.cs`;
  - `SpaceDefaultStatusPlan.cs` (verdicts), `SpaceDefaultStatusWriter.cs` (outcomes), `RestoreProjectStatusHandler.cs`;
  - `SpaceRoutes.cs`, `ProblemTypes.cs`, `ProblemResponse.cs`, `Program.cs`;
  - `SpaceStatusRow.razor`, `SpaceDefaultStatusSection.razor`, `RefusalCopy.cs`, `ClientCopy.resx`, `components.css`;
  - `RefusalBoundaryGateTests.SpaceDefaultStatuses.cs` and the `ProblemResponse` gate, `IsolationCases.cs`, `SmallViewportTests.cs`, `FakeYelloApi.SpaceDefaultStatuses.cs`;
  - `deferred-work.md`, `sprint-status.yaml`, and `ARCHITECTURE-SPINE.md` (the AD-16/AD-17 note).

### References

- `epics.md`: `:212` (AR-24), `:253-255` (UX-DR15-17), `:383` (coverage), `:2111-2115`, `:2247-2288` (this story), `:2290-2364` (6.5, 6.6 boundary).
- `prd.md`: `:37`, `:458`, `:464-469`, `:478-480`, `:487-490`, `:500-508`, `:718`, `:736-751`, `:807-808`, `:878`.
- `ARCHITECTURE-SPINE.md`: AD-2 `:85`, AD-16 `:186-193`, AD-17 `:195-199`, AD-18 `:201-206`, `:312`, `:333`, `:455`.
- `EXPERIENCE.md`: `:215`, `:257-260`, `:341-364`. `DESIGN.md`: `:256`, `:473-480`. Mockup `project-settings.html` `:151-197`.
- `SPEC.md`: `:132-133`, `:138-142`, `:203-204`.
- `test-design-qa.md`: `:264-277`, `:318-319`, `:405-430`.
- `implementation-readiness-report-2026-08-22.md`: `:243-247`, `:401`, `:503-506`, `:548`, `:1051`.
- `deferred-work.md`: `:114`, `:1022`, `:1030-1031`, `:1038-1039`, `:1045`, `:1056`, `:1061`, `:1066-1067`, `:1069`, `:1072`.
- Code: everything in "What already exists".

### ATDD artifacts - red-phase scaffolds already in the tree

Checklist: `_bmad-output/test-artifacts/atdd-checklist-6-4-remove-a-space-default-status-across-every-project.md`. Every
scaffold is written against each open question's recommended option, and a skip that rests on one names it.

| File | Cases | ACs | Today |
|---|---|---|---|
| `tests/Yello.Tests.Slices/Projects/ProjectEndpointTests.SpaceDefaultRemovalRace.cs` | 6 (1 theory): races 1-5, S-1 | AC4, AC6, AC10, AC17 | skipped `red:` |
| `tests/Yello.Tests.Slices/Projects/ProjectEndpointTests.SpaceDefaultRemoval.cs` | 18 (2 theories): minimum Slices cases 1-15 | AC1-AC5, AC7-AC9, AC11, AC12, AC14, AC15, AC17 | skipped `red:` |
| `tests/Yello.Tests.Slices/Projects/ProjectEndpointTests.SpaceDefaultRemovalScale.cs` | 1 (Nightly, Explicit) | AC16 | skipped `red:` |
| `tests/Yello.Tests.Slices/Statuses/EffectiveStatusSetSpaceRemovalTests.cs` | 7 (no database) | AC5, AC7, AC8 | **green, active** |
| `tests/Yello.Tests.Architecture/RefusalBoundaryGateTests.SpaceDefaultStatusRemoval.cs` | 4 gates | AC1-AC4, AC11 | skipped `red:` |
| `tests/Yello.Tests.Isolation/IsolationCases.SpaceDefaultStatusRemoval.cs` | 7 rows, both surfaces | AC3, AC11, AC12 | `PendingUntil: "6.4"` |
| `tests/Yello.Tests.Client/SpaceDefaultStatusRemovalTests.cs` | 11 (2 theories), fake API | AC3-AC5, AC11, AC13 | skipped `red:` |
| `tests/Yello.Tests.Client/SmallViewportTests.cs` | 1 added: `A_selected_column_removed_at_Space_level_leaves_no_selection_after_the_Board_is_read_again` | AC14 | **green, active** |

Supporting files: `Yello.Tests.Shared/SpaceStatusWire.cs` (stand-ins `Remove`, `RemoveOf`,
`StatusProjectDestinationRequired`, the body `RemoveBody`, the report reader `RemovalOf` on member `removal`),
`Yello.Tests.Slices/Projects/SpaceDefaultStatusWire.cs` (the removal on both surfaces, the Space's delta rows, the S-1
trigger on the definition delete), `Yello.Tests.Client/FakeYelloApi.SpaceDefaultStatusRemoval.cs` (the removal in AC5's
decision order, hold/release, `RemoveSpaceDefaultBehindThePage`), one hook in `FakeYelloApi.SpaceDeletion.cs` and one
spread line in `IsolationCases.All`. Swap every stand-in for the contract member and delete it.

**Green today.** `EffectiveStatusSetSpaceRemovalTests` pins AC7's invariant on the existing derivation (`Of` + `Without` +
`Reanchored`): a standing gate for "no second derivation". The pager case is the ledger `:1045` browser case (Q12): the
deterministic re-read exists (a reorder refused `409 task-position-unavailable` after the fake removed the column behind
the open Board), and it passes on 6.3's fix; it forces a second read serving the column again so it asserts what only
`ForgetSelectionThatLeft` provides. Close `:1045` on it.

**Activation order** (riskiest assumption first): (1) the lock set -
`A_Space_removal_queued_behind_a_Task_edit_into_the_default_counts_or_migrates_the_edited_Task`, then the
ProjectsPerSpace race, the queued Task edit, the queued add-back and the "removed the destination first" race; (2) the
plan's own unit tests with its type, keeping `EffectiveStatusSetSpaceRemovalTests` green; (3) the four contract gates;
(4) the core wire cases (unoccupied, report, accepted destination, blocked with and without an exception, anchored places,
removed/renamed Projects); (5) the refusals (Space-wide destination, exception, ignored exceptions, set-empty); (6) S-1,
add-back `404`, the readers; (7) Role, boundary, replay, then the 7 isolation rows live (GATE-003); (8) the browser cases,
then the Nightly scale case once to record its duration (Q15).

Activate one at a time, confirm it fails for the stated reason, then implement. A scaffold that is wrong is corrected and the correction recorded in the Debug Log, not deleted.

**Existing gates the implementation will turn red - amend them deliberately, in the same change:**

- `RefusalBoundaryGateTests.The_Space_Status_routes_declare_their_capability_serve_both_surfaces_and_consult_no_bound` (its exact route list).
- `RefusalBoundaryGateTests.No_route_or_request_under_the_Spaces_Statuses_offers_a_removal_a_reset_or_a_mode` (forbids
  "Remove" and "Destination"): rewrite it, do not delete it, so it still forbids a reset and a mode everywhere and a removal
  outside `…/remove`.
- The `ProblemResponse` parameter gates: `The_problem_body_appends_a_nullable_conflict_report_and_a_nullable_Project_list`
  and `The_owned_Spaces_refusal_has_a_stable_type_and_an_appended_identity_only_Spaces_member` (`RefusalBoundaryGateTests.DeleteAccount.cs`).
- The isolation route-coverage gate `IsolationCaseListTests.Every_Space_scoped_route_a_Token_reaches_…` until
  `ISO-TOKEN-OWN-OTHER-SPACE-SPACE-STATUS-REMOVE` is live; GATE-003 once 6.4 reaches review with any row still pending.
- The Client case `SpaceDefaultStatusesTests.Each_answer_sends_its_decision_once_and_is_announced(false)`, which asserts
  the "2 Projects kept their own names." copy AC13 rewords (Q11).
- The role-matrix theory already has a `ConfigureSpaceDefaultStatuses` row; it needs no new row unless it is changed to
  need one per route.

## Dev Agent Record

### Agent Model Used

Claude Opus 5.5 (bmad-dev-story, unattended story cycle)

### Debug Log References

- **Activation order followed** (lock set first). The race scaffolds were activated against the implementation and all six
  passed on the first run. Their red step is the lock-set plants below: plant 3 turns race 1 red and plant 4 turns race 5
  red, which shows they fail for the stated reason.
- **Scaffold corrections:**
  - `SpaceDefaultStatusRemovalTests.RowTextsAsync` (Client): Chromium's `innerText` of a `tr` includes every `option` of a
    `select` in it ("Beacon 2 Choose… Done Parked"). The helper now joins the text of the cells that hold no `select`. The
    picker's options are still asserted on their own, so nothing is weakened.
  - Stand-ins swapped for the contract members and deleted: `SpaceStatusWire.Remove`, `RemoveOf` and
    `StatusProjectDestinationRequired` became `SpaceRoutes.StatusRemove`, `SpaceRoutes.StatusRemoveOf` and
    `ProblemTypes.StatusProjectDestinationRequired`. The report member kept its recommended name, `removal`.
  - Red-phase remarks, `Skip` and `PendingUntil` removed from every scaffold file. The 7 isolation rows are live.
- **Gates amended deliberately:**
  - `The_Space_Status_routes_declare_their_capability_…` now lists `POST …/remove`.
  - `No_route_or_request_under_the_Spaces_Statuses_offers_a_removal_a_reset_or_a_mode` was rewritten and renamed
    `…offers_a_reset_or_a_mode_or_a_removal_outside_the_remove_route`. It still forbids a reset, a mode and `DELETE`
    everywhere, and every forbidden word in the add, rename and place requests.
  - Both `ProblemResponse` parameter gates append `Removal`.
  - The 6.3 Client case `Each_answer_sends_its_decision_once_and_is_announced(false)` now expects the "keep" copy with no
    count.
- **Plants.** Each was restored from saved content in a `finally` and run on its own as a filtered run. `git status` showed
  no residue afterwards.
  1. A blocked Project without an exception falls to its first Status. Caught (case 4, and the plan unit test).
  2. Projects that removed the Status are reported. Caught by case 2 (the report) and the plan unit test. Case 9 alone does
     not catch it, because a reported Project that moves no Task keeps its set unchanged.
  3. The removal skips the Project Tasks locks. Caught (race 1, both rows).
  4. The removal skips `ProjectsPerSpace`. Caught (race 5: the creation never blocks).
  5. No re-anchoring. Caught (case 10, and the plan unit test).
  6. The Project-left-empty check is skipped. Caught (case 8).
  7. The Space-wide picker pre-selects the first destination. Caught (the browser "nothing chosen" case).
  8. An exception picker pre-selects its first Status. Planted as a `selected` attribute on the first option in markup
     only, it did not take effect in the browser, so nothing was pre-selected and nothing was caught. Planted as a state
     default (the dialog passing the first Status as `Chosen`), it was caught (the browser blocked-table case).
  9. The client predicate admits Member. Caught by the matrix gate and by the browser absence case (member row).
  10. The add-back skips the current-default check and the `Remove` ops are kept. Caught (case 11: the removal itself fails
      on the definition's key). The current-default check on its own is defensive: once the ops are deleted, no `Remove`
      is left for the add-back to find.
- **Scale case (AC16).** Activated (Nightly, Explicit). Its one run in this phase was stopped by the host for low memory
  before it reported, and the cycle's rules say not to restart it. The seed size and duration are unrecorded. A ledger
  entry for Lee carries it, and the review's run owns the measurement.

### Completion Notes List

- The Space-level removal is built end to end:
  - Domain: the plan `SpaceDefaultStatusRemoval`, the `SpaceDefaultStatusRemovalPlan` records, three new verdicts and the
    port `ISpaceDefaultStatusRemovalStore`.
  - Infrastructure: `SpaceDefaultStatusRemovalStore`; `TaskStatusMigration`, lifted from `ProjectStatusStore` with no
    behaviour change (the 6.2 and 6.3 Status cases re-ran green); and the sorted multi-Project lock
    `ApplicationLocks.TakeProjectTasksAsync`.
  - Application: `RemoveSpaceStatusHandler`.
  - Contracts: `RemoveSpaceStatusRequest`, `SpaceStatusDestinationRequest`, `SpaceStatusOccupancyResponse`,
    `StatusChoiceResponse`, `ProblemTypes.StatusProjectDestinationRequired` and `ProblemResponse.Removal`.
  - Host: `POST /api/v1/spaces/{spaceId}/statuses/{statusId}/remove`.
  - Client: the Space settings "Remove" control, `SpaceStatusRemoveDialog` and `SpaceStatusExceptionRow`.
- Adding back a default after a Space removal answers `404`. `RestoreProjectStatusHandler` also requires a current default
  (AC9).
- The rename's "keep" announcement no longer states a count (AC13, ledger `:1067`).
- Decisions. Each is reversible and can be cut:
  - Q1 (a): `POST …/statuses/{statusId}/remove`, answering `200` with the defaults.
  - Q2 (a): the removal reports itself. `409 status-destination-required` carries `removal`. One new type,
    `status-project-destination-required`. `projects` is reused for `status-set-empty` and for an invalid exception.
  - Q3 (a): a sibling writer. The handler owns the extra locks and the count, and the 6.3 writer is unchanged.
  - Q4 (a): an unoccupied removal needs no destination. A destination sent anyway is still validated. A Project with no
    Task in the Status is never reported.
  - Q5 (a): StatusDefaults X, then ProjectsPerSpace, then ProjectTasks of the holders only, sorted by lock name.
  - Q6 (a): exceptions for unblocked, foreign or random Projects are ignored.
  - Q7 (a): a Project left empty is refused `409 status-set-empty`, naming it. Contradiction C1 is resolved by FR-25.
  - Q8 (a): no cancel in flight. Cancel and Escape work before commit only.
  - Q9 (a): the definition and every op of it are deleted. The other defaults keep their position gaps.
  - Q10 (a): the rename keeps its at-commit rule. Ledger `:1061` is closed with the reason.
  - Q11 (a): the "keep" copy has no count.
  - Q12 (a): the pager browser case is the green `SmallViewportTests` case. Ledger `:1045` is closed.
  - Q13 (a): the S-1 trigger is on the definition delete, the last statement.
  - Q14 (a): the commit stays enabled. Unset pickers are marked and the first is focused. "{m} destinations still needed"
    is shown as text beside the commit.
  - Q15 (a): a Nightly Explicit case, plus a ledger entry for Lee. The measurement is still pending (see the Debug Log).
  - A Project named twice in `exceptions` is treated as an unreadable body: a `400` decided at the endpoint, as the fake
    does, so the fake and the Host agree. The plan keeps the same guard.
  - The holders come from the defaults and deltas read after `StatusDefaults` X and `ProjectsPerSpace`, which keep both
    stable. Only the occupancy is read after the Tasks locks.
- Ledger dispositions are written for `:114`, `:1022`, `:1030`, `:1031`, `:1038`, `:1039`, `:1045` (closed), `:1056`
  (closed), `:1061` (closed, as a decision), `:1066` (noted), `:1067` (closed) and `:1072`. One new low entry for Lee
  covers the scale measurement.
- Architecture spine: additive 2026-11-22 notes under AD-16 and AD-17.

### File List

- Yello.Domain/Statuses/SpaceDefaultStatusRemoval.cs (new)
- Yello.Domain/Statuses/SpaceDefaultStatusRemovalPlan.cs (new)
- Yello.Domain/Statuses/ISpaceDefaultStatusRemovalStore.cs (new)
- Yello.Domain/Statuses/SpaceDefaultStatusPlan.cs
- Yello.Application/Statuses/RemoveSpaceStatus/RemoveSpaceStatusHandler.cs (new)
- Yello.Application/Statuses/SpaceDefaultStatusWriter.cs
- Yello.Application/Statuses/ProjectStatusDeltaDraft.cs
- Yello.Application/Statuses/ProjectStatusDeltaWriter.cs
- Yello.Application/Statuses/RestoreProjectStatus/RestoreProjectStatusHandler.cs
- Yello.Infrastructure/Persistence/SpaceDefaultStatusRemovalStore.cs (new)
- Yello.Infrastructure/Persistence/TaskStatusMigration.cs (new)
- Yello.Infrastructure/Persistence/ProjectStatusStore.cs
- Yello.Infrastructure/Persistence/ApplicationLocks.cs
- Yello.Infrastructure/SpaceContentStores.cs
- Yello.Contracts/Spaces/RemoveSpaceStatusRequest.cs (new)
- Yello.Contracts/Spaces/SpaceStatusDestinationRequest.cs (new)
- Yello.Contracts/Spaces/SpaceStatusOccupancyResponse.cs (new)
- Yello.Contracts/Spaces/SpaceRoutes.cs
- Yello.Contracts/ProblemTypes.cs
- Yello.Contracts/ProblemResponse.cs
- Yello.Host/Endpoints/SpaceDefaultStatusRemovalEndpoints.cs (new)
- Yello.Host/Program.cs
- Yello.Client/Components/SpaceDefaultStatusSection.Removal.cs (new)
- Yello.Client/Components/SpaceStatusRemoveDialog.razor (new)
- Yello.Client/Components/SpaceStatusExceptionRow.razor (new)
- Yello.Client/Components/SpaceStatusFocus.cs (new, moved out of the section's code-behind)
- Yello.Client/Components/SpaceDefaultStatusSection.razor
- Yello.Client/Components/SpaceDefaultStatusSection.razor.cs
- Yello.Client/Components/SpaceStatusRow.razor
- Yello.Client/Refusals/RefusalCopy.cs
- Yello.Client/Localisation/ClientCopy.resx
- Yello.Client/wwwroot/css/components.css
- tests/Yello.Tests.Slices/Statuses/SpaceDefaultStatusRemovalTests.cs (new)
- tests/Yello.Tests.Slices/Statuses/RemoveSpaceStatusHandlerTests.cs (new)
- tests/Yello.Tests.Slices/Projects/ProjectEndpointTests.SpaceDefaultRemoval.cs
- tests/Yello.Tests.Slices/Projects/ProjectEndpointTests.SpaceDefaultRemovalRace.cs
- tests/Yello.Tests.Slices/Projects/ProjectEndpointTests.SpaceDefaultRemovalScale.cs
- tests/Yello.Tests.Slices/Projects/SpaceDefaultStatusWire.cs
- tests/Yello.Tests.Shared/SpaceStatusWire.cs
- tests/Yello.Tests.Architecture/RefusalBoundaryGateTests.SpaceDefaultStatusRemoval.cs
- tests/Yello.Tests.Architecture/RefusalBoundaryGateTests.SpaceDefaultStatuses.cs
- tests/Yello.Tests.Architecture/RefusalBoundaryGateTests.DeleteAccount.cs
- tests/Yello.Tests.Isolation/IsolationCases.SpaceDefaultStatusRemoval.cs
- tests/Yello.Tests.Client/SpaceDefaultStatusRemovalTests.cs
- tests/Yello.Tests.Client/SpaceDefaultStatusesTests.cs
- tests/Yello.Tests.Client/FakeYelloApi.SpaceDefaultStatusRemoval.cs
- _bmad-output/implementation-artifacts/deferred-work.md
- _bmad-output/implementation-artifacts/sprint-status.yaml
- _bmad-output/planning-artifacts/architecture/architecture-YelloBMAD-2026-08-17/ARCHITECTURE-SPINE.md

## Change Log

- 2026-11-22: Story created (bmad-create-story). Status: ready-for-dev.
- 2026-11-22: Implemented (bmad-dev-story). Space-level removal across every Project: the domain plan, the lock set, the
  store, the handler, the route, the contracts and the Space settings dialog. Add-back hardened. The rename's "keep" copy
  no longer states a count. Ledger dispositions written. AD-16 and AD-17 notes added. Open questions Q1-Q15 decided as
  recommended. Status: review.
