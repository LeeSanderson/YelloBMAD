# Story 6.3: Define the Space default Statuses, and cascade a rename

Status: ready-for-dev

Epic: 6 — Shape Statuses per Project and retire work safely (third of six stories)
Story key: `6-3-define-the-space-default-statuses-and-cascade-a-rename`

**Requirements owned.** From the Story Coverage Index (`epics.md:503-506` of the readiness report; `epics.md:2207-2245`): **FR-24** (define the ordered default set; its removal half is 6.4's), **FR-27** (the add, rename and reorder half; the removal half is 6.4's), **UX-DR16** (the rename half of the report-and-ask cascade). Also carried here:

- **AD-16** (`ARCHITECTURE-SPINE.md:186-192`): a Status id survives a rename at both levels. The effective set is derived on read and stored nowhere. A Space-level edit must therefore reach every Project **through the derivation**, never by writing a per-Project copy.
- **AD-17 / S-2** (`test-design-qa.md:319`, P0): no Task holds a Status absent from its Project's effective set. No 6.3 operation takes a Status out of a set, so S-2 cannot newly fail here. Assert it after every case anyway: it is the standing gate.
- **FR-16** (`prd.md:338-354`): "Configure the Space's default Statuses" is Owner and Admin only. `Capability.ConfigureSpaceDefaultStatuses` already exists (`Yello.Domain/Memberships/Capability.cs:61`, matrix row `RoleCapabilityMatrix.cs:43`, gate row `RefusalBoundaryGateTests.cs:105`). **No endpoint declares it yet.** 6.3 is the first.
- **NFR-1 / FR-15 / AD-2 / AD-3** (I-1 to I-4, A-4, P0): every new route carries `{spaceId}` and gets isolation rows on both surfaces.
- **AD-18**: every write is idempotent through the pipeline.
- **UX**: `EXPERIENCE.md:53` (Space settings holds "default Statuses"; Owner, Admin), `:341-353` ("Space-level rename that collides"), `:363` ("The interface must not decide anything here"); mockup `ux-designs/ux-YelloBMAD-2026-08-18/mockups/project-settings.html` §2 (the rename-cascade dialog).

**Ledger entries owned (`deferred-work.md`). Close each one, or re-own it with a reason:**

| Line | Entry | Recommended disposition |
|---|---|---|
| `:1024` | A Project-added name can equal a later Space default's name (low, owner 6.3) | **CLOSE** (AC6, Q5): a Space add or rename that would put a name twice in any Project's set is refused, naming the Projects. |
| `:1051` | The Space defaults behind "Add back" are read once and never again (low, owner 6.3) | **CLOSE** (AC13, Q12): the Project editor re-reads the Space defaults with every set it renders. |
| `:1045` | The pager's stale-selection fix has no browser case (low, owner 6.3) | **Q11.** 6.3 adds no operation that takes a column off a Board. Write the case through the fake if a deterministic re-read path exists; otherwise re-own it to **6.4** with that reason. |
| `:550` | Space settings holds only Labels; remaining owner 6.3 (the Space's default Statuses) | **CLOSE** (AC11): the "Default Statuses" section exists. |
| `:114` | The first default Status name may not appear in any comment (S1135). Owner Epic 6 | **Keep open**, a standing trap for 6.3-6.6 (see Traps). |
| `:1030`, `:1031` | Name comparison vs collation; look-alike names (owner epic 6) | Leave open. 6.3 compares new names with the same `StatusName.Comparer`, so it neither fixes nor worsens them. Note that in the ledger. |
| `:1038`, `:1039` | 6.1 migration not safe for the old revision; `Down()` lossy (owner epic 6) | Leave open. 6.3 needs **no migration** (see "Schema"). Note that in the ledger. |

## What already exists — do not rebuild it

| Concern | Where it lives today | What 6.3 does with it |
|---|---|---|
| Default rows | `StatusDefinition` with `OwnerProjectId == null` (`Yello.Domain/Statuses/StatusDefinition.cs`): `Id` (stable, `ValueGeneratedNever`), `SpaceId`, `Name` (settable, max 100, `SQL_Latin1_General_CP1_CI_AS`), `Position` (int, settable), `CreatedAt` | **The Space-level edit writes these rows directly**: insert a default, set its `Name`, rewrite `Position`s. No new column, no new table. |
| Seeding | `DefaultStatusSet.Names` (three names, `Position` 0, 1, 2), written by `NewSpace` (`Yello.Domain/Spaces/NewSpace.cs:62`) at registration and at Space creation. Asserted by `ActiveSpaceEndpointTests.CreateSpace.cs:108` and `RegisterAccountHandlerTests` | **Unchanged.** AC10's "seeded as the three defaults" is already true; cite those tests, do not re-seed. |
| Default-name index | `StatusDefinitionConfiguration.cs`: unique `(SpaceId, Name) WHERE OwnerProjectId IS NULL`; unique `(SpaceId, OwnerProjectId, Name) WHERE OwnerProjectId IS NOT NULL`; alternate key `(SpaceId, Id)` | Backstop for two defaults sharing a name. The application checks first; a `2601`/`2627` the check missed answers `409 status-name-taken`, as `ProjectStatusStore` already does. |
| The one derivation | `EffectiveStatusSet.Of(defaults, delta)` (`Yello.Domain/Statuses/EffectiveStatusSet.cs`): defaults ordered by `Position` (stable on read order), then the delta's ops in `Sequence` order. `Rename` of a default yields `DefaultName` = the default's current name (cleared when the Project's name equals it exactly). `Place` keeps anchors; an anchor not in the set is ignored. `Check(after, namedStatusId)` refuses `SetEmpty` and `NameTaken` | **Most of FR-27 is already true by derivation.** A Space rename reaches every Project with no `Rename` op of that Status; a Space reorder reaches every Project with no `Place` op of that Status; a Space add reaches every Project. 6.3 must not write anything per Project for those. Reuse `Check` per Project for the name clash (Q5). Do not add a second derivation. |
| Reads | `IStatusDefinitionReader.ListAsync()` (defaults only, `Position` then `Id` in database order), `DeltaOfAsync(projectId)`; `ProjectStatuses.EffectiveOfAsync` (static extension, no cache) | Add **one** read of every Project's delta in the Space (owned definitions and operations, grouped by Project): two queries, not 50. |
| Project delta writer | `ProjectStatusDeltaWriter` (lock → read defaults + delta → plan on `ProjectStatusDeltaDraft` → `Check` the after-set → record → re-read), via `IProjectStatusStore.LockAsync` (plain read, `ApplicationLocks.TakeProjectTasksAsync`, re-read) | **Its lock must change** (Q6): before the Project's lock it takes the new Space-level Status lock in Shared mode, so a Project's name check and a Space-level name change never interleave. Nothing else in it changes. |
| Locks | `ApplicationLocks` (`Yello.Infrastructure/Persistence/ApplicationLocks.cs`): names carry the request's Space; `TakeAsync(resource, mode)` already supports `Shared` (`TakeMembershipsSharedAsync` is the precedent) | Add `StatusDefaults(spaceId)` = `Yello/StatusDefaults/{spaceId:D}` with an Exclusive and a Shared taker (Q6). |
| Space read | `GET /api/v1/spaces/{spaceId}/statuses` (`SpaceRoutes.Statuses`, `SpaceStatusEndpoints.MapListStatuses`, `ListStatusesHandler`) → `StatusResponse(Id, Name, Position)`, `ReadProjectsAndTasks` | **Unchanged in shape** (gate `The_Spaces_own_Status_read_keeps_its_shape`). The editor reads it; every write answers with the same list. |
| Project routes and contracts | `ProjectStatusRoutes` (collection, item, `place`, `remove`, `restore`), `AddProjectStatusRequest(Name, AfterStatusId)`, `RenameProjectStatusRequest(Name)`, `PlaceProjectStatusRequest(AfterStatusId)`, `ProjectStatusResponse(Id, Name, Source, DefaultName, TaskCount)` | The template for the Space routes (Q1). **Never change `ProjectStatusResponse` or `StatusResponse` in a breaking way** (C-1, C-2). |
| Endpoints pattern | `ProjectStatusEndpoints.cs` (`JsonRequestBody.ReadAsync`, `ProjectStatusAnswers.Refusal(outcome)`, `BoundaryProblems.NotAvailable()`, `201` + `Location` for an add), split into `ProjectStatusRemovalEndpoints.cs` for S1200 | Copy the shape into `SpaceStatusEndpoints.cs` (or a sibling `SpaceDefaultStatusEndpoints.cs`). |
| Problem types | `ProblemTypes.cs`: `invalid-status-name` (400), `status-name-taken` (409), `status-set-empty` (409), `invalid-status-position` (400) | Reuse all four. Add `status-rename-conflict` (409) (Q2). |
| Problem body | `ProblemResponse(Type, Errors, Capability, Bound, Limit, Spaces)` (`Yello.Contracts/ProblemResponse.cs`). Precedent for a list in a refusal: `Spaces` on `account-owns-spaces` (UX-DR36) | Add additive, nullable members for the conflict report and the name clash (Q2, Q5). |
| Space settings page | `Yello.Client/Pages/SpaceSettings.razor`: sections keyed by `SectionKey` (`Name`, `Labels`, `Memberships`, `Invitations`, `Ownership`, `Delete`), each behind its own predicate; the link's audience is `SpaceCapabilities.PermitsSpaceSettings` = the union of `Sections` (`SpaceCapabilities.cs:48-60`), gated by `The_clients_Space_settings_predicate_is_the_union_of_its_sections` | Add a `DefaultStatuses` section between `Name` and `Labels` (EXPERIENCE.md:53 order). Add its predicate to `Sections`. The audience does not change (Owner, Admin). |
| Client pieces | `StatusDeltaEditor.razor` (+ `.Removal.cs`), `StatusDeltaRow.razor` (inline rename, Move up/down with `aria-describedby`, `LiveAnnouncement`, `InlineErrorRegion`), `StatusRemoveDialog.razor` (dialog, focus trap both ways, in-flight region), `LabelSection.razor` (a Space settings section with add/rename), `StatusNameRule`, `StatusCapabilities`, `RefusalCopy`, `ClientCopy.resx` | Reuse. Build the section from these. A row component may be shared with `StatusDeltaRow` only if that stays under the analyzer caps (S1200/S1541); otherwise a new `SpaceStatusRow`. |
| Client predicate | `StatusCapabilities.PermitsConfigureProjectStatusDelta` + gate `The_clients_Project_Status_delta_predicate_agrees_with_the_capability_matrix` | Add `PermitsConfigureSpaceDefaultStatuses` and a twin gate. |
| Project editor's add-back list | `StatusDeltaEditor.Removal.cs:61` `LoadSpaceDefaultsAsync` reads `SpaceRoutes.StatusesOf` once on load | AC13: re-read it whenever the editor renders a new set. |
| Board, pager, picker, List | All drawn from the server's per-Project answer | **They follow a Space-level change on their next read with no change.** No push exists (AD-9 is Task-level); do not add one. |
| Race and invariant helpers | `DeleteWire.HoldProjectRangeAsync` (holds an app lock by name in a test transaction), `TaskWire.WaitUntilBlockedOnALockAsync`, `WireDatabase.TasksOutsideTheirProjectsSetSql` (S-2, knows `Remove`), `WireDatabase.DeltaOperationCountInAsync`, the S-1 trigger pattern `ActiveSpaceEndpointTests.DeleteSpace.cs:268-305` | Reuse. Add a holder for the new lock name. |
| Isolation | `IsolationCases.cs:66` `ISO-DIRECT-STATUSES` (GET, asserts exactly SA's three Status ids), `ISO-AGGREGATE`, `ISO-RECORD-KIND`; `IsolationCases.ProjectStatuses.cs`, `IsolationCases.StatusRemoval.cs`; `Every_Space_scoped_route_a_Token_reaches_has_a_row_...` | New rows for the three writes. **Never change SA's default set in a shared world**: three existing rows assert it exactly (Traps). |

**What is genuinely new in 6.3:**

1. Three Space-level writes on the defaults: add (optionally at a position), rename (with the cascade decision), reorder.
2. The rename conflict report: every Project that renamed that Status itself, with its name for it and its Task count, and one decision for all of them.
3. A Space-wide name check: no write may leave any Project's effective set holding a name twice (closes `:1024`).
4. A Space-level Status lock, and the Project delta writer taking it shared.
5. The "Default Statuses" section in Space settings, with the cascade dialog.
6. The Project editor re-reads the Space defaults (closes `:1051`).

**Explicitly not in this story:**

- Removing a Space default, its Space-wide destination and per-Project exceptions, and any Task migration (6.4). No remove route, control or `Remove` handling at Space level. The never-empty guard for the default set is built and unit-tested here, reachable in 6.4 (Q7).
- Moving Tasks between Projects (6.5, 6.6).
- Showing, in the Space editor, which Projects diverge before a rename is attempted (Q13).
- A push of Space-level changes to open Boards.
- No migration (no schema change), no new NuGet package, no new project. No commit, push, branch, Azure login or deploy. The orchestrator commits.

## Story

As an Owner or Admin,
I want to change the Statuses every Project starts from, and to decide what happens where a Project has diverged,
so that a Space-wide change never silently overwrites a deliberate local one.

## Acceptance Criteria

1. **Add a default; every Project gets it** (FR-24, FR-27). An Owner or Admin adds a Status to the Space defaults (name, and optionally the default it goes after). It is a new `StatusDefinition` with `OwnerProjectId = null`. Every Project in the Space shows it in its effective set on the next read, including Projects that have added, renamed, placed or removed **other** Statuses. **No row is written for any Project**: the `StatusDeltaOperation` count of the Space is unchanged by the add. Without a position it goes last in the Space order. In a Project it takes its Space-order slot among the defaults, so it appears before any Status the Project added and has not placed (Q8).

2. **Members and Viewers cannot change the defaults** (FR-16, FR-24, UX-DR27, C-6). Every write route declares `ConfigureSpaceDefaultStatuses`. A Member's or a Viewer's write is refused `403` with the pipeline's refusal record, on Session and Token. In the client the section's editing controls and the cascade dialog are **absent from the DOM** for Member and Viewer. Space settings stays Owner/Admin only (its existing redirect is unchanged). A client predicate is held to the matrix by a gate.

3. **A rename reaches every Project that has not renamed that Status** (FR-27, AD-16). Renaming a default writes the default row's `Name` only. Its id is unchanged, and every Task's `StatusId`, every Board column route and every Project op that names it keep working. The new name shows in every Project with no `Rename` op of that Status, **including Projects that placed it** (reordered it) and Projects that renamed **other** Statuses. Asserted with: P-plain (no delta), P-placed (`Place` of that Status), P-other (renamed a different default). A Project that removed the Status is unaffected now, and if it adds the Status back, it returns under the new name (6.2's add-back, unchanged).

4. **Conflicts are reported, naming every Project and its name** (FR-27, UX-DR16; `EXPERIENCE.md:347-353`). A Project **conflicts** when its delta holds a `Rename` of that Status whose name differs (ordinal) from both the Status's current Space name and the requested new name (Q3). When at least one Project conflicts and the request carries no decision, the rename is refused `409 status-rename-conflict`, nothing is written, and the problem body names **every** conflicting Project: its id, its name, its current name for that Status, and how many of its Tasks hold that Status (Q2). The report is computed under the lock (AC9).

5. **One decision for all of them** (FR-27; `prd.md:503`). The request's decision (`replaceProjectNames: true | false`) applies to every Project that conflicts **at commit**, in the one request:
   - `true`: those Projects' `Rename` ops for that Status are deleted, so they show the new Space name.
   - `false`: their ops stay, so they keep their own names. Their editor now shows "renamed here (was {new name})".
   - Either way the rename applies to every non-conflicting Project, and the answer is `200` with the Space defaults.
   - A `Rename` op whose name equals the old or the new Space name exactly is not a conflict (Q3). It is deleted by the rename, so that Project follows the Space name.
   - A decision sent when nothing conflicts is ignored.

   The deletion of ops and the default's rename are **one transaction**: a failure forced after the op deletes leaves the default's name and every Project's op unchanged (S-1 style; Q10). Anchor that case on the same request succeeding once the trigger is dropped.

6. **No name twice in any set** (FR-25 "deterministic", 6.1 AC10; ledger `:1024`; Q5). An add or a rename is refused `409 status-name-taken` when, after it, the default set **or any Project's effective set** would hold the name twice, compared with `StatusName.Comparer`. The check uses `EffectiveStatusSet.Of` and `Check` on each Project's after-set. When Projects are the reason, the problem body names them (id and name). Nothing is written. Examples to assert: a Project that added "Parked", then a Space add of "parked"; a Project that renamed another default to "Review", then a Space rename to "Review" (under `true` and under `false` for a conflicting Project whose own name for the Status would remain). Renaming a default to its own name with a different case is allowed (6.1's rule).

7. **Names**. Trimmed, 1-100 characters (`StatusName.TryNormalise`), else `400 invalid-status-name`. Unique among the defaults, compared case-insensitively. An index refusal the check missed answers `409 status-name-taken`.

8. **Reorder: every Project that has not placed that Status follows** (FR-27). Placing a default after another default (or first) rewrites the defaults' `Position`s to the new order (0..n-1). Every Project with no `Place` op of **that** Status shows it in the new Space order. A Project with a `Place` op of it keeps its own placement. Asserted with P-plain and P-placed. A Status-sort List and Task creation's first Status follow the new order in P-plain. An anchor that is not a default of this Space (another Space's, a Project-added Status, itself) is `400 invalid-status-position`. A Status in the path that is not a default is the boundary `404`.

9. **Serialised with every Project's Status writes** (Q6; `TESTING-CONVENTIONS.md:102-180`). Space-level writes take `Yello/StatusDefaults/{spaceId}` Exclusive. Every Project delta write (add, rename, place, remove, restore) takes it **Shared** before the Project's lock. Lock order is always StatusDefaults → ProjectTasks. Proved deterministically by holding the Status lock in a test transaction and waiting on `LCK_M_%`, never by timing:
   - A Space add of "X" and a Project add of "X" both queue on the held lock. Released, exactly one is admitted and the other is `409 status-name-taken`. No set holds "X" twice.
   - With the lock held, a Project rename of a default (to "Live") and a Space rename of the same default (to "Shipped", no decision) both queue. Released in either order, the outcome is consistent: either the Space rename was first and the Project then holds "Live" over "Shipped", or the Project rename was first and the Space rename is `409 status-rename-conflict` naming that Project. Never a Space rename that missed the Project's committed op.
   - Two Project delta writes in different Projects do not wait on each other (both Shared). Assert that neither is blocked while the other holds.

10. **The default set is never empty, and a new Space starts with three** (FR-24). A new Space's defaults are `DefaultStatusSet.Names` in order (already true; cite `ActiveSpaceEndpointTests.CreateSpace.cs:108`). The domain refuses any Space-level change that would leave the default set empty. It is proved by a unit test now, and becomes reachable over HTTP with 6.4's removal (Q7). No 6.3 route can remove a default.

11. **The Space settings section** (FR-24, UX-DR16, UX-DR27, UX-DR31, UX-DR42, NFR-9; mockup `project-settings.html` §2):
    - A "Default Statuses" section in Space settings, between Space name and Labels, for Owner and Admin (`SectionKey.DefaultStatuses`; its predicate joins `SpaceCapabilities.Sections`).
    - Rows in Space order: name, inline rename (with an accessible name), "Move up" / "Move down" (named for their Status through `aria-describedby`, with a polite announcement of the new position), and an "Add a Status" form. **No remove control** (6.4).
    - **The cascade dialog**, opened by a `409 status-rename-conflict`. Heading: "{n} Projects renamed this Status themselves." Line: "You're renaming the Space default "{old}" to "{new}". These Projects gave it their own name:". Then a table (Project, its name for this Status, Tasks), "Every other Project takes the new name either way.", and two actions, "Rename these too" and "Keep their names". **Neither is pre-focused and neither is the `Enter` default.** Focus moves to the dialog's heading. Escape or a Cancel control closes it, sends nothing and returns focus to the rename field. The focus trap wraps both ways (6.2's fix).
    - **Done:** a polite announcement. With no conflicts: "Renamed "{old}" to "{new}" in every Project." With replace: "Renamed "{old}" to "{new}" in every Project." With keep: "Renamed "{old}" to "{new}". {n} Projects kept their own names." Focus stays on the row.
    - **Refused:** the existing inline error region with localised copy. `status-name-taken` with Projects says which Projects already use the name.
    - Copy in `ClientCopy.resx`, sentence case, with singular and plural forms (6.2's deferred `:1052` was the lack of a singular: do not repeat it).
    - Logical properties, usable at 320px. Purged on Space switch, sign-out and Account switch.

12. **Isolation** (NFR-1, FR-15, AD-2, AD-3; I-1 to I-4, A-4). Each write route carries `{spaceId}`. Each of the following answers the boundary's byte-identical `404 not-available`, writes nothing, and records what the boundary rules say:
    - a foreign Space,
    - another Space's Status id in the caller's own Space path,
    - a Project-added Status id (owned by a Project of the caller's own Space) in the path.

    An anchor from another Space in the body is `400 invalid-status-position`, identical to a random id. The conflict report and the name-clash list name only Projects of the request's Space: assert it with a foreign Project that renamed a Status of the **same name**. Rows go in `IsolationCases` on both surfaces, including the `ISO-TOKEN-OWN-OTHER-SPACE-*` rows, and are live before review (GATE-003).

13. **The Project editor stays true after a Space change** (ledger `:1051`). `StatusDeltaEditor` re-reads the Space defaults whenever it renders a set from the server: the first load, every write's answer, and every `404` re-read. So the "Add back" list never shows a stale name or a default the Space no longer has. A browser case renames a removed default at Space level (fake API) and sees the "Add back" entry's new name after the next editor write.

14. **Idempotent, and the answer is the truth** (AD-18). The three writes go through the pipeline's `Idempotency-Key`. A replay returns the original response and applies nothing again. A cascade replay deletes nothing a second time. Add answers `201` with `Location` on the collection; rename and place answer `200`. Each answers with the Space's whole default list as `StatusResponse` rows, in order.

15. **Derived, never materialised** (AD-16, AR-23; `epics.md:2243-2245`). After every Space-level write, every Project's effective set is still `EffectiveStatusSet.Of(defaults, delta)`. No table, column or cache holds a Project's set. The existing schema gate (`ProjectEndpointTests.StatusSchema.cs`, "no `%EffectiveStatus%`", one delta table) stays green unamended. A Space add writes no delta row. A Space reorder writes no delta row. A Space rename deletes only the `Rename` ops AC5 names.

16. **S-2 holds after every case** (AD-17, P0). Assert `WireDatabase.TasksOutsideTheirProjectsSetSql` = 0 after each Slices case this story adds.

## Tasks / Subtasks

- [ ] **Task 1 — Domain: the Space-level plan** (AC1, AC3-AC8, AC10, AC15)
  - [ ] A pure planner in `Yello.Domain/Statuses/` (name is the dev's, for example `SpaceDefaultStatusPlan`): given the defaults, every Project's delta (with the Project's id) and one change (add, rename + decision, place), return:
    - the defaults after the change,
    - the conflicting Projects (rename only; Q3),
    - the `Rename` ops to delete (AC5),
    - the Projects whose after-set would hold a name twice (Q5).
    
    Compute every Project's after-set with `EffectiveStatusSet.Of(defaultsAfter, deltaAfter)` and judge it with `EffectiveStatusSet.Check(after, statusId)`. **No second derivation.**
  - [ ] Reorder: produce the defaults' new `Position`s from an anchor (reuse the list logic of `EffectiveStatusSet.Placements` or a small sibling; never a third ordering algorithm for the same job).
  - [ ] The never-empty guard for the default set (AC10, Q7): a check that refuses an empty default set, ready for 6.4.
  - [ ] Unit tests (no database), in `tests/Yello.Tests.Slices/Statuses/` (for example `SpaceDefaultStatusPlanTests.cs`):
    - an add reaches a plain Project, one that removed another default, and one that added Statuses (slot before unplaced added Statuses);
    - a rename reaches plain, placed and other-renamed Projects;
    - conflicts: an own name differs → conflict; an own name equal to the old name or the new name (ordinal) → not a conflict, op deleted;
    - decision `true` deletes the conflicting ops and `false` keeps them;
    - a removed Status is never a conflict;
    - a name clash with an added Status, and with another renamed default, under both decisions;
    - reorder: P-plain follows, P-placed keeps its place;
    - an empty default set is refused.
- [ ] **Task 2 — Lock and reads** (AC9, AC4)
  - [ ] `ApplicationLocks.StatusDefaults(spaceId)` = `Yello/StatusDefaults/{spaceId:D}`, with Exclusive and Shared takers. Write in its remarks the lock order (StatusDefaults → ProjectTasks) and why Task writers never take it: no Space-level change in 6.3 takes a Status out of a set, so a Task's Status check is unaffected. 6.4 takes it Exclusive before every ProjectTasks it migrates.
  - [ ] `ProjectStatusStore.LockAsync`: take StatusDefaults **Shared** before `TakeProjectTasksAsync`. Update `IProjectStatusStore`'s remarks.
  - [ ] Reader: every Project's delta in the active Space (`IStatusDefinitionReader.DeltasAsync()` or similar): owned definitions and operations, two queries, grouped by Project, no `Where SpaceId`.
  - [ ] Reader: for one Status, the Task count per Project (one grouped statement), and the names of the Projects the report names. Use the existing Project read if one fits.
- [ ] **Task 3 — Infrastructure: the Space-level store** (AC1, AC3, AC5, AC8, AC14)
  - [ ] A Domain port (for example `ISpaceDefaultStatusStore`) and an Infrastructure store: `LockAsync` (StatusDefaults Exclusive), add a default, set a default's name, rewrite positions, and delete named `Rename` ops (one set-based `ExecuteDelete` over `(ProjectId, StatusId, Kind = Rename)` for the listed Projects).
  - [ ] Statement order in one unit of work: delete the ops, then rename the default (or the reverse; both are in one transaction). Catch `2601`/`2627` from the default-name index as `NameTaken`, and detach so nothing stays pending (copy `ProjectStatusStore`).
  - [ ] No `Where SpaceId` (the policy narrows it). No raw SQL unless needed; any raw SQL lives in Infrastructure with parameters.
  - [ ] Register in `SpaceContentStores.cs` / `InfrastructureServices.cs` as the Project store is.
- [ ] **Task 4 — Application: the writer and three slices** (AC1, AC3-AC8, AC14)
  - [ ] `Yello.Application/Statuses/SpaceDefaultStatusWriter.cs` (mirrors `ProjectStatusDeltaWriter`): lock → read defaults and every delta → plan → refuse or record → re-read and answer the defaults.
  - [ ] Slices: `Yello.Application/Statuses/AddSpaceStatus/`, `RenameSpaceStatus/`, `PlaceSpaceStatus/` (names are the dev's).
  - [ ] Decision order for a rename, under the lock:
    1. Status not a default → `NotFound`.
    2. Name unchanged (ordinal) → `Written` with no change.
    3. Name clash among the defaults → `NameTaken`.
    4. Conflicts and no decision → `RenameConflict` (with the report).
    5. Clash in any Project's after-set → `NameTaken` (with the Projects).
    6. Otherwise record.
  - [ ] An outcome type for Space writes (or extend `ProjectStatusWriteOutcome`; the dev's call), carrying the conflict report and the clashing Projects.
  - [ ] Handler tests with fakes (no database).
- [ ] **Task 5 — Contracts and Host** (AC2, AC4, AC6, AC12, AC14; Q1, Q2)
  - [ ] Routes on `SpaceRoutes` (or a new `SpaceStatusRoutes`): `POST /api/v1/spaces/{spaceId}/statuses` (`AddSpaceStatusRequest(string? Name, Guid? AfterStatusId)`), `PATCH …/statuses/{statusId}` (`RenameSpaceStatusRequest(string? Name, bool? ReplaceProjectNames)`), `POST …/statuses/{statusId}/place` (`PlaceSpaceStatusRequest(Guid? AfterStatusId)`). Path helpers for each.
  - [ ] `ProblemTypes.StatusRenameConflict` = `urn:yello:problem:status-rename-conflict` (409).
  - [ ] `ProblemResponse` gains two nullable, additive members (Q2, Q5): `Conflicts` (a list of `{ ProjectId, ProjectName, Name, TaskCount }`) and `Projects` (a list of `{ Id, Name }`). Reuse an existing identity record for the second if one fits. The problem writer emits them only on their type.
  - [ ] Endpoints: `RequiresCapability(Capability.ConfigureSpaceDefaultStatuses)`, both surfaces, idempotent through the pipeline. An unreadable body → `400 invalid-status-name` (add, rename) or `400 invalid-status-position` (place). Register them in `Program.cs`.
  - [ ] Gates: the routes declare their capability and serve both surfaces; the role-matrix theory gains a `ConfigureSpaceDefaultStatuses` representative (`ActiveSpaceEndpointTests.RoleMatrix.cs`; `The_matrix_theory_covers_exactly_the_capabilities_endpoints_declare` will go red until it does); the problem type is stable; `StatusResponse` keeps its shape; no route or contract under the Space's Statuses offers a remove, a reset or a mode. If a gate pins `ProblemResponse`'s members, amend it deliberately.
- [ ] **Task 6 — Client** (AC2, AC11, AC13)
  - [ ] `StatusCapabilities.PermitsConfigureSpaceDefaultStatuses` (Owner, Admin) plus a matrix gate. Add it to `SpaceCapabilities.Sections` (`The_clients_Space_settings_predicate_is_the_union_of_its_sections` must stay green).
  - [ ] `SpaceDefaultStatusSection.razor` (name per the dev) in `SpaceSettings.razor`, keyed `(SectionKey.DefaultStatuses, SpaceId)`, between `Name` and `Labels`. Rows, rename, move, add (reuse `StatusDeltaRow` only if the caps allow).
  - [ ] `StatusRenameCascadeDialog.razor` (name per the dev), per AC11. Reuse `StatusRemoveDialog`'s dialog, focus and trap pattern. "Keep their names" is not a `destructive-confirm`, and neither button gets the `button-primary` "safe default" treatment over the other (mockup note). Follow DESIGN.md `dialog` (`:223-229`).
  - [ ] `RefusalCopy` maps `status-rename-conflict` (it opens the dialog rather than rendering an error) and `status-name-taken` with Projects.
  - [ ] `StatusDeltaEditor`: re-read the Space defaults with every set it renders (AC13).
  - [ ] `FakeYelloApi.SpaceDefaultStatuses.cs`: the three writes, the `409` with conflicts, the `409` name clash, and the Project sets and Board columns derived from the fake's defaults so a Project page follows.
- [ ] **Task 7 — Ledger and docs**
  - [ ] Write the disposition of every owned ledger entry in `deferred-work.md` (CLOSED / UPDATED / RE-OWNED, with the reason). Leave none blank.
  - [ ] AD-16: a dated, additive note: a Space-level rename writes the default's name and, on a "replace" decision, deletes the conflicting Projects' `Rename` ops; an add and a reorder write no Project row; a Space-level Status lock (Shared for Project writes) serialises name checks. Additive only; no rule changes.
- [ ] **Task 8 — Tests** (see Testing requirements)

## Dev Notes

### The shape of the story in one paragraph

6.1 and 6.2 made a Project's set `Of(defaults, delta)`. The Space defaults are ordinary `StatusDefinition` rows with no owner, and nothing has ever edited them after seeding. 6.3 edits them. Because the set is derived, most of FR-27 needs no per-Project work: a new default appears in every Project, a renamed default shows its new name wherever no `Rename` op overrides it, and a reordered default moves wherever no `Place` op pins it. What 6.3 really adds is the two places where a Space change meets a Project's own choice:
- the **rename conflict**: a Project's own `Rename` of the same Status, which is reported, decided once, and then either kept or deleted;
- the **name clash**: a Space name that a Project's set already holds for another Status, which is refused and named.

Both need every Project's delta read under one lock. That lock is the main new piece of machinery.

### Dominant finding: FR-27's add/rename/reorder half is already true by derivation. The work is the conflict, the clash and the lock.

- **Do not write per-Project rows for an add, a plain rename or a reorder.** The derivation already delivers them. A per-Project write would be a materialised copy (AD-16) and would be wrong for Projects created later.
- **The only per-Project write is deleting `Rename` ops**: the conflicting Projects' ops on "replace", and ops equal to the old or the new name in every case (Q3). Nothing is inserted per Project.
- **The name clash is the real hazard.** 6.1 checks a Project's names against its own set under the Project's lock. A Space write changes names in up to 50 sets at once. Without a common lock, a Project add of "X" and a Space add of "X" can each pass their own check and commit a set holding "X" twice. Hence the Space-level lock, Shared for Project writes (Q6).
- **No migration.** Every column 6.3 writes exists (`StatusDefinition.Name`, `.Position`, new default rows; `StatusDeltaOperation` deletes). So no AD-16 schema note is needed beyond the additive behaviour note, and ledger `:1038`/`:1039` are untouched.

### Locking (RCSI)

- `Yello/StatusDefaults/{spaceId}`: Space-level writes take it Exclusive. Every Project delta write takes it Shared, inside `IProjectStatusStore.LockAsync`, **before** `ProjectTasks`. Two Project writes in different Projects never wait on each other.
- **Lock order:** StatusDefaults → ProjectTasks → Task rows → Labels. Nothing may take StatusDefaults after ProjectTasks. Task writers (create, edit, move, delete) and Project deletion never take StatusDefaults, so they cannot form a cycle with it.
- **Why Task writers need nothing:** no 6.3 change takes a Status out of a set, so a Task's Status, valid before, stays valid. A Space reorder can change a Project's first Status between a creation's read and its write. That is harmless: the read happens under ProjectTasks, and both orders are valid sets.
- Project deletion concurrent with a cascade: the Space write's op deletes and the Project deletion's op deletes may meet on the same rows. One waits for the other, and neither takes a lock the other holds in reverse order (the Space write touches only `StatusDeltaOperation` rows and one `StatusDefinition` row). State this in the store's remarks.
- **For 6.4** (record in the lock's remarks): a Space-level removal must take StatusDefaults Exclusive and then every affected Project's ProjectTasks in a fixed order (by id).

### Conflict semantics (recommended; Q3)

Before a rename of default S from `old` to `new`, for each Project P whose delta holds `Rename(S, n)`:

| `n` compared (ordinal) | What happens |
|---|---|
| `n == new` | Not a conflict. The op is deleted; P follows the Space name, which is already its name. |
| `n == old` | Not a conflict. P's editor shows it as a plain Space default (6.1 clears `DefaultName` when equal), so to the Admin it has not renamed it. The op is deleted; P follows. |
| otherwise (including a case-only difference) | **Conflict.** Reported. Deleted on `true`, kept on `false`. |

A Project whose delta removes S has no `Rename` op of S (6.2 deletes it on removal), so it is never reported. If it adds S back later, S returns under the new name, and 6.2's add-back checks the name.

### Open questions

Each has a recommendation, which is the default. The dev phase decides each as a reversible, recorded decision, in Completion Notes and the Change Log. **None is marked [LEE]:**
- no rule of the spine changes (AD-16 gets an additive note);
- there is no schema change and no migration;
- no RLS rule changes (no new table);
- the new lock is an Infrastructure mechanism inside the existing AD-2 naming discipline (names carry the request's Space);
- the contract changes are additive (C-1, C-2).

1. **Route shapes** (AC1, AC3, AC8). **(a) Recommended:** the Project routes' shapes, on the Space's existing collection: `POST …/statuses` `{ name, afterStatusId? }` → `201`, `PATCH …/statuses/{statusId}` `{ name, replaceProjectNames? }` → `200`, `POST …/statuses/{statusId}/place` `{ afterStatusId }` → `200`. Each answers the defaults as `StatusResponse` rows. (b) A separate `…/status-defaults` collection. Cost: two paths for one set of rows, and the existing read would no longer sit beside its writes.
2. **How the conflict is reported** (AC4). **(a) Recommended:** `409 status-rename-conflict` with a `Conflicts` list in the problem body (id, Project name, its name, Task count), computed under the lock. The client opens the dialog straight from it. This is the `account-owns-spaces` precedent: a refusal that must name things carries them. (b) A read route that lists, per default, the Projects that renamed it, with the client re-reading after a bare `409`. Cost: a new read and a second round trip, for information that is only needed at the moment of the rename.
3. **What counts as a conflict** (AC4, AC5). **(a) Recommended:** the table above. A `Rename` op equal to the old or the new name exactly is not a conflict and is deleted. (b) Every `Rename` op of S is a conflict. Cost: it reports Projects whose editor shows them as not renamed, and asks the Admin about a Project already showing the new name.
4. **A decision meeting a changed conflict set** (AC5). **(a) Recommended:** the decision applies to every Project conflicting **at commit**. The PRD makes it one policy choice for all of them, and the lock makes the window tiny. The answer is the defaults. (b) The request echoes the Project ids it was shown, and a different set at commit is refused `409` again. Cost: another round trip for a race between two Admins. Add a low ledger entry if (a) is taken.
5. **The name clash** (AC6; ledger `:1024`). **(a) Recommended:** refuse `409 status-name-taken`, naming the Projects in a `Projects` list in the problem body (null when the clash is among the defaults). (b) Admit it and let those Projects show the name twice. Cost: it breaks 6.1 AC10 and the Board. (c) Skip the clashing Projects. Cost: impossible for an add (identity reaches every Project), and a silent decision for a rename.
6. **The lock** (AC9). **(a) Recommended:** a new `StatusDefaults(spaceId)` lock: Exclusive for Space writes, Shared for Project delta writes, ordered before ProjectTasks. (b) Space writes take every Project's ProjectTasks lock in id order. Cost: they block every Task write in the Space for the duration. They also need a Project-list read before locking, which a new Project can slip past (harmless today, not for 6.4). And 50 locks per write.
7. **"Removing its last Status is refused" in a story with no removal** (AC10). **(a) Recommended:** the domain guard and its unit test now; 6.4 adds the HTTP case. This is the same move as 6.1's Q7. (b) A remove route that only ever refuses. Cost: a route with no other behaviour, and 6.4 then has to rewrite it.
8. **Where a new default lands** (AC1). **(a) Recommended:** last in Space order unless `afterStatusId` is given (Add, then Place, in one write, as 6.1 did). In a Project it takes its slot among the defaults, which puts it before any added Status the Project has not placed. (b) Always last in every Project. Cost: it needs a per-Project `Place` row, which is a write per Project that AD-16's derivation exists to avoid.
9. **How a reorder is stored** (AC8). **(a) Recommended:** rewrite all defaults' `Position` to 0..n-1 in the new order, in one unit of work. It is the Space's own small set, not a Project's effective set, so this is not materialisation. (b) Gap-based positions. Cost: complexity for a set of a handful of rows.
10. **Atomicity proof for the cascade** (AC5). **(a) Recommended:** one S-1-style case: a test-only trigger on `StatusDefinition` that throws for the renamed id, so the op deletes are rolled back. Anchor it on success once the trigger is dropped. (b) None, because it is one transaction by construction. Cost: no proof that the deletes and the rename share it.
11. **The pager browser case** (ledger `:1045`). 6.3 adds no operation that takes a column off a Board. **(a) Recommended:** look for one deterministic path through the fake: a narrow Board with a column selected, the fake removes that Status (6.2's fake), and a Task send the fake refuses sets `_rereadBoard`, so `UseRereadBoardAsync` runs `ForgetSelectionThatLeft`. If one case does it, write it in `SmallViewportTests` and close the entry. If not, re-own it to **6.4**, with the reason that only a removal takes a column away and 6.3 has none. (b) Re-own it now, without trying.
12. **The Project editor's Space defaults** (AC13; ledger `:1051`). **(a) Recommended:** re-read with every set the editor renders. That is one extra `GET` per write, which is cheap. (b) Re-read only on load and after a `404`. Cost: a stale entry after the Admin's own writes.
13. **Showing divergence in the Space editor before a rename** (UX-DR15 parity). **(a) Recommended:** not in 6.3. The ACs require the report at the rename, which the `409` delivers. Add a low **UX** ledger entry: "The Space defaults editor does not show which Projects renamed a default until a rename is attempted." (b) Add a per-default "renamed in n Projects" chip. Cost: a new read, for something no AC asks.
14. **Cancel in the cascade dialog** (AC11). **(a) Recommended:** Escape and a Cancel control close it, send nothing, and return focus to the rename field. That is a dismissal, not a third answer: nothing is renamed anywhere. The mockup shows only the two answers, but a dialog needs a way out (DESIGN.md `dialog`). (b) No Cancel. Cost: the Admin must choose to learn they cannot back out.

### Contradictions found (recorded; do not resolve beyond what is stated)

- **C1.** `StatusDefinition`'s remarks say the editor, removal and cascade are "Epic 6's, at stories 6.1 to 6.4", and `DefaultStatusSet`'s remarks say "Seeded here, owned by Epic 6". Both remain true. Do not rewrite them except to stay accurate.
- **C2.** The epics put "removing its last Status is refused" in 6.3, but there is no Space-level removal until 6.4. See Q7.
- **C3.** The mockup's cascade dialog sits on a Project settings mockup page (`project-settings.html` §2), but its chrome reads "Space settings". The rename is a Space-level act, so the dialog lives in Space settings.
- **C4.** EXPERIENCE.md:53 lists Space settings as "Memberships and Invitations, Space name, default Statuses, Labels, ownership, deletion". The built page orders them Name, Labels, Members, Invitations, Ownership, Delete. Place "Default Statuses" between Name and Labels. Do not reorder the others.

### Architecture compliance (must)

- **AD-21 ring** (`tests/Yello.Tests.Architecture/AllowedReferenceEdges.cs`):
  - Domain: the planner and the store port in `Yello.Domain/Statuses/`.
  - Application: slices in `Yello.Application/Statuses/<Slice>/`.
  - Infrastructure: the store, the reads and the lock in `Yello.Infrastructure/Persistence/`.
  - Host: endpoints in `Yello.Host/Endpoints/`.
  - Contracts: in `Yello.Contracts/Spaces/`.
  - No EF type in Domain or Application; no ASP.NET type in Application or Domain.
- **AD-1 / AR-3:** no Role read and no Membership check in a slice. The capability is declared on the endpoint.
- **AD-2:** no `Where SpaceId`. Raw SQL only in Infrastructure, with parameters. The lock name carries the request's Space (`ScopedSpaceId`).
- **AD-3 / AD-20:** an in-Space not-found answers the boundary body and writes no refusal record.
- **AD-16:** one derivation; no per-Project materialisation; ids survive rename.
- **AD-17:** S-2 after every case.
- **AD-18:** idempotent through the pipeline.
- **AD-19 / C-1, C-2:** additive contract changes only (`ProblemResponse` gains nullable members; `StatusResponse` unchanged).
- **AR-34:** problem+json with stable `urn:yello:problem:*` codes; ids generated by the application (`IIdentifierGenerator`); `DateTimeOffset` in UTC.
- **Code comments** (CLAUDE.md): explain why; no story numbers, AC/Q/ledger ids, dates or history; no XML doc that restates a name.

### Traps

- **S1135 (ledger `:114`).** The first default Status name may never appear in a comment, a doc comment or a Razor comment. Write "the first default" in prose and `DefaultStatusSet.Names[0]` in code. Test string literals are fine.
- **Shared isolation worlds.** `ISO-DIRECT-STATUSES`, `ISO-AGGREGATE` and `ISO-RECORD-KIND` assert exactly SA's three Status ids and names. A new isolation row must never add, rename or reorder SA's defaults in the shared world. Seed Space-level writes in a Space of their own (6.1's correction: a fresh Account's Space, seeded once per world). Do not push B past its 50-Project bound.
- **A Space rename swap** (A↔B) is two requests and the default-name index is checked per statement, so a single request never swaps. That is fine: nothing asks for a swap.
- **Positions tie.** The reader breaks ties by `Id` in the database's order. After a reorder, rewrite every default's `Position` so no tie is left.
- **`DefaultName` in the Project editor** is derived from the current Space name. After a "keep" decision, a Project shows "renamed here (was {new name})". That is correct; do not "fix" it.
- **S1200 / S1541** split classes in 5.2, 5.4, 6.1 and 6.2. `SpaceSettings.razor` and the new section will grow, so plan the dialog and the row as separate components from the start. `StatusDeltaEditor` is 436 lines.
- **The editor's write path** drops a write while another is in flight (ledger, owner UX). The section must make an in-flight rename visible and must not send a second rename on a second press.
- `TreatWarningsAsErrors`: 0 warnings is a gate. `--no-build` runs stale binaries; rebuild after every change. A leftover `Yello.Host.exe` locks `bin/Debug` (`Get-Process Yello.Host`).
- **Plants** must compile under the analyzers (two plants in 6.1 and two in 6.2 did not, and proved nothing). Restore each from saved content in a `finally`; never `git checkout --`, `git restore` or `git reset`.

### Library / framework

No new package or project. No web research was needed: every API used is already in the codebase.

| Area | What it uses |
|---|---|
| EF Core 10.0.11 | `ExecuteDeleteAsync`, `ExecuteUpdateAsync`, tracked insert of a default, the existing filtered unique index |
| SQL Server 2025 | RCSI, `sp_getapplock` in `Shared` and `Exclusive` modes, transaction owner |
| ASP.NET Core 10.0.11 | Minimal APIs, Asp.Versioning.Http 10.0.0 |
| Blazor WebAssembly 10.0.11 | The client, and Localization 10.0.11 for the copy |
| Tests | xunit.v3 4.0.0, Testcontainers 4.6.0, Playwright 1.62.0, ArchUnitNET 0.13.3 |

### Testing requirements

Follow `tests/TESTING-CONVENTIONS.md`:
- traits `:44-95`,
- rules `:97-207` (races `:102-180`),
- browser cases `:209-305`,
- the shared container `:621-690`,
- the zero-tests trap `:706`,
- isolation `:790-911`.

General rules:

- **Traits.** `Suite`, `Priority`, `Requirement`, `Tier`. The isolation, race and S-2 cases are **P0**. `Requirement` values: FR-24, FR-27, FR-16, AD-16, AD-17, AD-18, NFR-1, UX-DR16.
- **No new container-backed test class.** Slices cases go in new partials of `ProjectEndpointTests`: `ProjectEndpointTests.SpaceDefaults.cs` and `.SpaceDefaultsRace.cs`. Domain and handler tests use no database. Isolation rows go in `IsolationCases.SpaceDefaultStatuses.cs`. The browser class `SpaceDefaultStatusesTests.cs` uses `FakeYelloApi` (not container-backed). AC13's case extends `ProjectSettingsTests`.
- **Seed through the product.** Raw SQL only for catalogue reads, S-2, op counts, the S-1 trigger and holding the lock.
- **Every absence has an anchor and is seen red under a plant.**
- **Memory budget.** One `dotnet test` at a time. Iterate on one project filtered to the affected classes. Develop ends with one background full run with `--max-parallel-test-modules 1` and `-p:YelloSkipBrowserSuite=true`.

**Minimum cases (Slices):**

| # | Case |
|---|---|
| 1 | Add a default: every Project (plain, one that removed another default, one with an added Status) shows it in its Space slot; the Board has one more column; the Space's delta-op count is unchanged. |
| 2 | Add at a position (after a default). |
| 3 | Rename with no conflict: P-plain, P-placed and P-other show the new name; the id, the Tasks' `StatusId` and the Board column route are unchanged. |
| 4 | Rename with conflicts and no decision → `409 status-rename-conflict` naming every conflicting Project, its name and its Task count; nothing changed. |
| 5 | Same with `replaceProjectNames: true` → conflicting Projects show the new name; their `Rename` ops are gone; the others are untouched. |
| 6 | Same with `false` → conflicting Projects keep their names and show the new name as `DefaultName`; non-conflicting Projects take the new name. |
| 7 | A Project's own name equal to the old or the new name exactly → not reported; it follows. A Project that removed the Status → not reported; adding it back shows the new name. |
| 8 | Name clash with a Project's added Status, and with another renamed default → `409 status-name-taken` naming the Projects; nothing changed. Same-name rename differing only in case → allowed. |
| 9 | Name refusals: blank, overlong → `400 invalid-status-name`; a clash among the defaults → `409 status-name-taken` with no Projects. |
| 10 | Reorder: P-plain follows; P-placed keeps its own place; Task creation in P-plain takes the new first default; the List Status sort follows. |
| 11 | Reorder anchor that is not a default (another Space's, a Project-added Status, itself) → `400 invalid-status-position`; a non-default Status in the path → `404`. |
| 12 | Member and Viewer get `403` on each write, on both surfaces. |
| 13 | Foreign Space, another Space's Status in the path, a Project-added Status in the path → `404`, byte-identical. |
| 14 | A foreign Project that renamed a same-named Status is never named in a report or a clash list. |
| 15 | Idempotent replay of each write; a cascade replay deletes nothing again. |
| 16 | The S-1-style cascade case (Q10). |
| 17 | Races (AC9): Space add vs Project add of one name; Space rename vs Project rename of the same default; two Project writes in different Projects do not block each other. |
| 18 | S-2 after every case; AC15's schema gate unchanged and green. |

**Minimum plant table** (restore each in a `finally`):

| # | Plant | Expected red |
|---|---|---|
| 1 | The Space rename also writes a `Rename` op into every Project | cases 3 and 15 (op counts), AC15 |
| 2 | Conflicts ignore the decision (always keep) | case 5 |
| 3 | Every `Rename` op counts as a conflict | case 7 |
| 4 | The clash check looks only at the defaults | case 8 |
| 5 | Project delta writes skip the Shared Status lock | case 17 (the Space-vs-Project race) |
| 6 | Project delta writes take it Exclusive | case 17 (two Projects block each other) |
| 7 | Reorder writes a `Place` op per Project | case 10 (P-placed) and AC15 |
| 8 | The report names Projects outside the request's Space (the policy bypassed in the report's read) | case 14 |
| 9 | The client dialog pre-focuses "Rename these too" | the browser no-default case |
| 10 | The client predicate admits Member | the matrix gate and the browser absence case |

**Browser cases (fake API):**
- The section is absent for Member and Viewer.
- An Owner adds, renames and moves a default, and each is announced.
- A rename with conflicts opens the dialog, which names each Project with its name and count; focus is on its heading; neither action is focused or the `Enter` default; Escape sends nothing and returns focus.
- "Rename these too" and "Keep their names" each send the decision and announce.
- A name clash names the Projects inline.
- A Project's Board shows the renamed column after the change.
- The Project editor's "Add back" entry shows the new Space name (AC13).
- Usable at 320px.

### Previous story intelligence (6.1, 6.2)

- **The shape that works:** one writer that locks, reads under the lock, plans on a draft, checks every after-set with `EffectiveStatusSet.Check`, records, and answers the truth. Do the same at Space level.
- **Re-anchoring** (6.1 review, 6.2 removal): a change in order replays wrongly unless the placed Statuses are re-anchored. 6.3's Space reorder does **not** touch Project `Place` ops: a Project's anchors are its own choice, and FR-27 says it keeps them.
- **Owned names:** renaming an added Status writes its definition's name (6.1 review). Space-level renames touch only defaults.
- **Scaffolds corrected before:**
  - catalogue `STRING_AGG` needs `COLLATE DATABASE_DEFAULT`;
  - read over the wire inside the host block;
  - `GetCollation()` needs the design-time model;
  - isolation rows must not push B past its bound, and must not change SA's defaults;
  - every Token-reachable route needs an `ISO-TOKEN-OWN-OTHER-SPACE-*` row;
  - a record with a list member compares by reference (compare a projection);
  - a browser wait on `.board-column` `Nth(n)` is satisfied by the loading skeleton (wait on a heading).
- **Gates that went red by design before:** ARCH-503 (Project routes; 6.3 adds none under a Project), the role-matrix theory, `No_Project_request_contract_carries_anything_but_a_name` (Projects namespace only; put the Space requests in `Yello.Contracts.Spaces`), `SpaceIsolationSchemaTests` (new tables; 6.3 adds none).
- **6.2's client lessons:**
  - the pressed commit must stay until the progress region holds focus;
  - the focus trap must wrap both ways;
  - a control that removes itself must say where focus goes;
  - plural and singular copy.
- **Plants:** run them in small groups. A host killed for low memory skips the harness's `finally`, so check `git status` for residue.

### Git intelligence

- `0c017f7` story 6.2 file, `e9c39fb` its ATDD scaffolds, `e4c7cc6` its implementation, `23caa85` its review close. 6.1 was `6c6eb9a`, `bca3a5f`, `221b03e`, `4eb4b5f`.
- The cycle per story: story file → ATDD scaffolds (`Skip = "red: ..."`, `atdd-checklist-<key>.md`, `PendingUntil` isolation rows live before review) → implementation → review. The orchestrator commits each phase.
- The latest migration is `20261121190936_StatusDeltaSequenceUnique`. 6.3 expects none.

### Project Structure Notes

- **New (expected):**
  - Domain: `Yello.Domain/Statuses/SpaceDefaultStatusPlan.cs` (or similar), `ISpaceDefaultStatusStore.cs`.
  - Application: `Yello.Application/Statuses/SpaceDefaultStatusWriter.cs`, `AddSpaceStatus/`, `RenameSpaceStatus/`, `PlaceSpaceStatus/`.
  - Infrastructure: `Yello.Infrastructure/Persistence/SpaceDefaultStatusStore.cs`.
  - Contracts: `Yello.Contracts/Spaces/AddSpaceStatusRequest.cs`, `RenameSpaceStatusRequest.cs`, `PlaceSpaceStatusRequest.cs`, and the conflict entry record.
  - Host: `Yello.Host/Endpoints/SpaceDefaultStatusEndpoints.cs` (if `SpaceStatusEndpoints` would grow past its caps).
  - Client: `Yello.Client/Components/SpaceDefaultStatusSection.razor`, `StatusRenameCascadeDialog.razor`, possibly `SpaceStatusRow.razor`.
  - Tests: as listed above, plus `RefusalBoundaryGateTests.SpaceDefaultStatuses.cs`.
- **Updated (expected):**
  - Domain: `IStatusDefinitionReader.cs` (every delta), `IProjectStatusStore.cs` (remarks).
  - Infrastructure: `ApplicationLocks.cs`, `ProjectStatusStore.cs` (Shared lock), `StatusDefinitionReader.cs`, `SpaceContentStores.cs`.
  - Contracts: `SpaceRoutes.cs`, `ProblemTypes.cs`, `ProblemResponse.cs`.
  - Host: `SpaceStatusEndpoints.cs`, the problem writer, `Program.cs`.
  - Client: `SpaceSettings.razor`, `SpaceCapabilities.cs`, `StatusCapabilities.cs`, `StatusDeltaEditor.razor` / `.Removal.cs`, `RefusalCopy.cs`, `ClientCopy.resx`, `components.css`.
  - Tests: `RefusalBoundaryGateTests.cs` (the client predicate gate), `ActiveSpaceEndpointTests.RoleMatrix.cs`, `IsolationCases.cs`, `FakeYelloApi*.cs`, `ProjectSettingsTests.cs`, possibly `SmallViewportTests.cs`.
  - Docs: `deferred-work.md`, `sprint-status.yaml`, the AD-16 note in `ARCHITECTURE-SPINE.md`.

### References

- `epics.md`: `:469-477` (epic 6 notes), `:2111-2115`, `:2207-2245` (this story), `:2247-2288` (6.4, the boundary of scope).
- `prd.md`: `:455-458` (§4.7), `:462-468` (FR-24), `:492-506` (FR-27), `:897-898` (assumptions 6, 7).
- `ARCHITECTURE-SPINE.md`: AD-2, AD-3, AD-16 (`:186-192`), AD-17 (`:194-198`), AD-18, AD-21, the capability map `:454`.
- `SPEC.md`: `:132`, `:141-142`, `:272`.
- `EXPERIENCE.md`: `:53`, `:69`, `:215`, `:258-260`, `:339-363`.
- `DESIGN.md`: `:223-229` (dialog), `:322`, `:480-482`.
- Mockup: `project-settings.html` §2 (lines 124-150).
- `test-design-qa.md`: `:264-277` (I-1 to I-13), `:302-303` (A-4, A-5), `:318-319` (S-1, S-2).
- `implementation-readiness-report-2026-08-22.md`: `:238-246`, `:503-506`, `:657-658`.
- `deferred-work.md`: `:114`, `:550`, `:1024`, `:1030-1031`, `:1038-1039`, `:1045`, `:1051`, `:1056` (6.4's add-back of a vanished default; not this story's).
- Code:
  - `EffectiveStatusSet.cs`, `StatusDeltaOperation.cs`, `ProjectStatusDelta.cs`, `StatusDefinition.cs`, `DefaultStatusSet.cs`, `StatusName.cs`, `IStatusDefinitionReader.cs`, `IProjectStatusStore.cs`;
  - `ProjectStatusDeltaWriter.cs`, `ProjectStatusDeltaDraft.cs`, `ProjectStatusSet.cs`, `ProjectStatuses.cs`, `ListStatusesHandler.cs`;
  - `ProjectStatusStore.cs`, `StatusDefinitionReader.cs`, `StatusDefinitionConfiguration.cs`, `ApplicationLocks.cs`;
  - `SpaceStatusEndpoints.cs`, `ProjectStatusEndpoints.cs`, `ProjectStatusAnswers.cs`;
  - `SpaceRoutes.cs`, `ProjectStatusRoutes.cs`, `ProblemTypes.cs`, `ProblemResponse.cs`, `StatusResponse.cs`;
  - `SpaceSettings.razor`, `SpaceCapabilities.cs`, `StatusCapabilities.cs`, `StatusDeltaEditor.razor`, `StatusDeltaEditor.Removal.cs:40-80`, `StatusDeltaRow.razor`, `StatusRemoveDialog.razor`, `LabelSection.razor`, `BoardView.Pager.cs:53`, `BoardView.Paging.cs:288-310`;
  - tests: `ProjectEndpointTests.StatusRace.cs`, `DeleteWire.HoldProjectRangeAsync`, `WireDatabase.cs`, `ActiveSpaceEndpointTests.DeleteSpace.cs:268-305`, `ActiveSpaceEndpointTests.CreateSpace.cs:108`, `IsolationCases.cs:66`, `RefusalBoundaryGateTests.ProjectStatuses.cs`.

## Dev Agent Record

### Agent Model Used

### Debug Log References

### Completion Notes List

- Ultimate context engine analysis completed - comprehensive developer guide created.

### File List

## Change Log

- 2026-11-21: Story created (bmad-create-story). Status: ready-for-dev.
