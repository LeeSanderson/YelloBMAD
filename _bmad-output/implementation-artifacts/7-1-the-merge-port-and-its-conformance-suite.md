---
baseline_commit: ae9b494f3b03215a7709ef5684fadfc072ce56fd
---

# Story 7.1: The merge port and its conformance suite

Status: review

Epic: 7 — Write the same Task at the same time, and lose access the moment access ends (the first of eight stories)
Story key: `7-1-the-merge-port-and-its-conformance-suite`

**Requirements owned.** Story Coverage Index (`epics.md:386`): **AR-18** (= spine **AD-12**), **AR-40a**, **NFR-4**. Also carried:

- **AR-18 / AD-12** (`epics.md:206`, `ARCHITECTURE-SPINE.md:160-164`):
  - exactly one interface, `ITextMergeStrategy`, with exactly one registered implementation;
  - no domain, application or sync code references a concrete merge type;
  - the port's contract is an executable conformance suite that encodes FR-31, FR-33 and NFR-4;
  - the suite is written **before** any implementation and passes **before** any implementation merges;
  - the expected implementation is a plain-text sequence CRDT in `Yello.Merge`, one source, compiled to WebAssembly for the client and native for the server.
- **AR-40a** (`epics.md:228`, spine Deferred row `:471`): the text merge algorithm must be closed during implementation and not carried silently. It is admissible only by passing this suite. Whole-field last-writer-wins (LWW) cannot pass.
- **FR-31** (`prd.md:551-559`), the merge-level consequences only:
  - two Users editing different parts both retain their changes;
  - two Users editing the same region arrive at an identical final text, and that text is the one persisted.
  - "No merge prompt, lock or stale warning" belongs to the editor (7.4, M-10). "A Viewer cannot enter" belongs to 7.3 and 7.4.
- **FR-33** (`prd.md:570-577`), the merge-level consequences only:
  - changes made while disconnected are applied on reconnection and appear exactly once;
  - others' changes made during the disconnection are present.
  - "Reconciliation cannot complete → told explicitly" (M-8) is 7.6's.
- **NFR-4** (`prd.md:710-714`):
  - identical text within 2 s of the last edit;
  - at least 10 simultaneous editors;
  - up to 5 minutes disconnected reconciles without loss or duplication.
- **Test design:**
  - **M-1, M-2, M-3, M-5, M-9** (`test-design-qa.md:290-298`, P0, UNIT), plus the UNIT halves of **M-4** and **M-6**. M-7 and M-8 are INT and belong to 7.6.
  - **A-8** (`:306`, P0, ARCH).
  - **R6** (`test-design-architecture.md:152-155`, `:490-504`, score 6). Write the suite with no candidate in the repository, cite FR-31, FR-33 and NFR-4 clause by clause, assert that LWW **fails** it, and add a property-based interleaving harness.
  - Entry criterion (`test-design-qa.md:230`): the suite is written, and green against the falsification, *before* any candidate is added.
  - Exit criterion (`:244`): the merge conformance suite is green, with LWW demonstrably failing it.
- **AR-35** (`epics.md:223`): merge conformance is one of the four release-gating suites.

**Not in scope, so later stories own it:**

- the `TaskDescriptionChange` log, the projector, compaction and SM-5 (7.2, AR-19);
- `/sync`, leases and frames (7.3);
- the editor, the 16 ms and 300 ms budgets in the browser, server admit or reject over the wire, and Viewer exclusion (7.4);
- Presence (7.5);
- the reconnection UI, M-7 and M-8 (7.6);
- revocation (7.7, 7.8).

7.1 builds the port so that each of those stories uses it **without changing the interface** (Q4).

**Ledger entries owned (`deferred-work.md`).** No open entry names story 7.1. The entries owned by "epic 7" (`:582`, `:864`, `:1095`, `:1102`, `:1103`, `:1126`) concern realtime Board push and editing sessions, which are 7.3, 7.4 and 7.6 territory. **Leave them.** 7.1 adds one entry (Q11): where the server-side projector reaches the strategy. Owner: 7.2.

## What already exists — do not rebuild it

| Concern | Where it lives today | What 7.1 does with it |
|---|---|---|
| `Yello.Merge` project | `Yello.Merge/Yello.Merge.csproj` references `Yello.Contracts` only. It holds `AssemblyMarker.cs`. | Add the port, the implementation and the factory here. **Add no project reference.** The ring row is `["Yello.Merge"] = ["Yello.Contracts"]`. Update `AssemblyMarker`'s `<remarks>`: it says no implementation exists and AR-40a is open, and both become false. |
| `Yello.Tests.Merge` project | `tests/Yello.Tests.Merge/Yello.Tests.Merge.csproj` references `Yello.Merge` and `Yello.Contracts`, and xunit.v3. It has no tests and carries `--ignore-exit-code 8`. | Add the suite. **Remove the `--ignore-exit-code 8` PropertyGroup** in the same change, because `TestingConventionTests` fails a project that has tests and keeps the switch. Rewrite the csproj's stale comments ("Story 7.1 writes the suite; AR-40a is still open"). |
| Ring gate | `tests/Yello.Tests.Architecture/RingDependencyTests.cs:180` (`Merge_types_depend_on_nothing_but_Contracts`) and `AllowedReferenceEdges.cs:129-130`, `:149`, `:259` (Merge forbidden from `PersistenceAndTransport` packages) | Unchanged. Merge must stay free of EF Core, ASP.NET Core, SqlClient and Aspire, because it ships in the browser payload. |
| Host → Merge and Client → Merge edges | `Yello.Host.csproj:47`, `Yello.Client.csproj:43` | Already wired. **Use them** for the two registrations (AC8). |
| Client singleton ban | `ClientStaticStateGateTests.No_client_service_is_registered_as_a_singleton` (`:63`) | The client registers the strategy **scoped**, never as a singleton. Keep the strategy stateless (AC2). |
| Release-gating strictness | `ReleaseGatingSuiteTests.StrictSuites` (`:22`) lists Isolation and Revocation | Add `Yello.Tests.Merge` (AC10). AR-35 makes it a gate, so no ignored exit code of any value. |
| Zero-test switch gate | `TestingConventionTests` (`--ignore-exit-code 8` only in suites without tests) | Satisfied once the switch comes off. |
| Trait vocabulary | `TESTING-CONVENTIONS.md:44-73`: `Suite`, `Priority`, `Requirement`, `Assumption`, `Tier` | Cases carry `Suite=Merge`, `Priority=P0`/`P1` and `Requirement` (`FR-31`, `FR-33`, `NFR-4`, `AR-18`, `AD-12`). The large-budget property run is `Tier=Nightly` and `Explicit = true`. |
| Timing collection pattern | `BoundaryDurationCollection`, `TimingCollection` (`DisableParallelization`) | Every merge timing case goes in a `DisableParallelization` collection of its own (CLAUDE.md, "Timing tests need..."). |
| `MembershipChanged` seam, `Yello-Role` header, `ActiveSpaceState.RegisterPurge` | Host and Client | **Not touched.** They are 7.3 and 7.7. |

## Story

As the team about to build collaborative editing,
I want one merge interface whose contract is an executable suite written before any implementation,
So that two stories cannot choose different merge semantics and whole-field last-writer-wins cannot enter by the back door.

## Acceptance Criteria

1. **One port** (AR-18, AD-12, A-8; Q1, Q2, Q4). Exactly one interface named `ITextMergeStrategy` exists in the production assemblies. It is declared in `Yello.Merge`, because only that project is visible to both the Host and the WebAssembly client (Q1). Its contract is algorithm-agnostic: no CRDT, Lamport, tombstone or LWW concept appears in a public member name. It provides everything 7.2-7.6 need, so none of them has to change the interface (Q4):
   - create an empty replica bound to a replica identity;
   - apply a local insert or delete by text index and obtain the change it produced;
   - apply a remote change idempotently;
   - read the current plain text;
   - encode and decode a change to and from bytes;
   - say which changes a replica has, and produce the changes another replica lacks (the reconnection exchange);
   - encode a replica's whole state and load it again (the server rebuilds from the log, AD-14; compaction, 7.2);
   - withdraw a local change the server rejected (AD-11, AR-20; Q4).

2. **One implementation, which nobody names** (AR-18, AD-12, A-8; Q2, Q3).
   - Exactly one non-abstract production type implements `ITextMergeStrategy`. It lives in `Yello.Merge` and is `internal sealed`.
   - It is reached only through one public factory member in `Yello.Merge`, for example `TextMerge.CreateStrategy()`, whose declared return type is the interface.
   - The strategy holds no mutable state. All document state lives in replicas it creates, so one instance can serve every document, on any thread, as a Host singleton or a Client scoped service.
   - No type in `Yello.Domain`, `Yello.Application`, `Yello.Infrastructure`, `Yello.Host` (sync code included) or `Yello.Client` depends on the concrete type or on any other `Yello.Merge` type that implements a port member. Only the interface, its value types and the factory are visible to them.

3. **The suite exists before the implementation** (AD-12, R6; `test-design-qa.md:230`; Q5).
   - The conformance cases are written against `ITextMergeStrategy` only. They reach the candidate through one binding point in the test project, and the cases themselves never name a production type.
   - **The ATDD phase commits the suite, the falsification double (AC5) and the port with no candidate implementation.** The cycle commits per phase, so the git history then holds a commit where the suite exists, LWW fails it and no candidate exists. That is the record AD-12 asks for.
   - The dev phase adds the candidate and makes the suite green without editing a case's assertions. If a case must change, the Change Log records why, in requirement terms, never in "the algorithm does X" terms.

4. **Every case cites its clause.** Each conformance case carries a `Requirement` trait naming FR-31, FR-33 or NFR-4, plus `AR-18` and `AD-12`. Its name states the behaviour in requirement language, for example `Two_editors_changing_different_parts_of_a_description_both_keep_their_changes`. The cases are:
   - **M-1, FR-31 different regions, 4 cases.** For each case, both changes survive and the result is the same text at every replica, whatever the delivery order:
     1. two inserts in different paragraphs;
     2. an insert and a delete that do not overlap;
     3. two deletes that do not overlap;
     4. an insert at the start and an insert at the end.
   - **M-2, FR-31 same region, 4 cases.** For each case, every replica holds identical text after full delivery, in both delivery orders, and the text is the same whichever replica is asked:
     1. two inserts at the same index;
     2. overlapping deletes;
     3. an insert inside a range another replica deletes;
     4. a replace (delete then insert) against a concurrent insert in the same range.
   - **Exactly once, FR-33.** Each of these leaves the text unchanged:
     - delivering a change twice;
     - delivering a change after a reconnection exchange that already carried it;
     - re-sending a replica's pending changes after a reconnect.

     Changes keep their identity across encode, decode and resend.
   - **M-5, NFR-4 ten editors.** Ten replicas each make concurrent inserts and deletes, and every change is delivered to every replica in a different order per replica. All ten texts are identical, and every inserted character that no replica deleted is present exactly once.
   - **M-6, FR-33 and NFR-4 disconnection, 3 cases.** One replica is partitioned while it and the others edit a workload sized for 5 minutes (Q7). It then reconciles through the reconnection exchange alone, with no full-text transfer.
     1. Its own changes are present exactly once.
     2. Others' changes are present.
     3. Two replicas that were partitioned at the same time, from each other and from the rest, both reconcile.
   - **M-4, NFR-4 2 s, UNIT half** (Q6).
     - Convergence needs no extra coordination round. Once each replica has received every change, the texts are identical with no further message.
     - On the native runtime, applying the M-6 workload's reconnection exchange stays within the budget Q6 sets.
     - The browser half belongs to 7.4 (R15).
   - **Round trip.** Each of these yields a replica that behaves identically to the original:
     - a change's encoding decodes to an equal change;
     - a replica's encoded state loads into a replica with the same text and the same "has";
     - the loaded replica converges with the others when it receives further changes.
   - **Admit or reject, AD-11.** Malformed bytes, a truncated payload and an unknown encoding version are each refused with a typed result or exception the caller can branch on (Q8), and leave the replica's text and "has" unchanged. A local change withdrawn after a rejection leaves the replica converged with a replica that never saw it.
   - **Text indexes** (Q9). An index or length outside the text is refused, and so is a position that would split a UTF-16 surrogate pair. Text containing non-BMP characters, such as emoji, converges without producing an unpaired surrogate at any replica.

5. **Whole-field last-writer-wins demonstrably fails** (AD-12, M-3, R6; `epics.md:2424-2427`). The test project holds a `WholeFieldLastWriterWins` double that implements `ITextMergeStrategy`: each change carries the whole text plus a timestamp, and the later one wins.
   - A falsification test runs **the same case bodies** as the conformance suite against it. It asserts that at least the M-1 different-regions cases and the M-6 own-and-others'-changes-present case **fail** with an assertion failure, not an exception from the double.
   - The test also asserts that the double **passes** a single-editor sequential case. That proves the failures come from the merge semantics rather than from a broken double, so the falsification is not vacuous.
   - The double lives only in `Yello.Tests.Merge`. The "exactly one implementation" gate (AC6) scans the production assemblies and therefore does not count it.

6. **Architecture gates** (A-8; Architecture suite). Each absence assertion is proved against a planted signal first (`TESTING-CONVENTIONS.md:192-195`), and the Dev Agent Record records the plant results. Gates:
   1. exactly one production type named `ITextMergeStrategy`, and it is an interface in `Yello.Merge`;
   2. exactly one non-abstract production implementation, in `Yello.Merge`, not public;
   3. no type outside `Yello.Merge` depends on that implementation;
   4. `ITextMergeStrategy` is registered in exactly one place per composition root (Host `Program.cs`, Client `Program.cs`), always from the factory;
   5. no production type outside `Yello.Merge` constructs a replica except through the interface.

   Plants:
   - a second interface named `ITextMergeStrategy` in another assembly;
   - a second implementation;
   - a Host type naming the concrete type;
   - a second registration.

   Restore every plant by saved content in a `finally`, never with `git checkout` (CLAUDE.md).

7. **Property-based interleaving** (M-9, R6, NFR-4; Q10). A seeded, hand-written harness in `Yello.Tests.Merge` adds **no new package**: FsCheck and CsCheck would each need Lee's approval (CLAUDE.md, "New dependencies").
   - It generates random sessions of 2 to 10 replicas with random inserts, deletes and replaces, random delivery orders, duplications and partitions with later reconnection exchanges.
   - It asserts three things:
     - convergence: all texts are equal once every change is delivered;
     - no lost insert: every inserted character not deleted by a change causally or concurrently known is present exactly once;
     - no resurrected delete.
   - Each failure message prints its seed and the minimal operation trace. A fixed seed list is replayable from a single test.
   - The PR run uses a fixed budget (Q10). A larger budget runs as `Tier=Nightly`, `Explicit = true`.

8. **Both sides run the same merge** (`epics.md:2429-2431`; Q3, Q12).
   - The Host registers `ITextMergeStrategy` from the factory as a singleton in `Program.cs`. The Client registers it from the same factory as **scoped** in its `Program.cs`, which keeps the singleton ban.
   - A case in `Yello.Tests.Client`, which already publishes the client, asserts that the **published** client carries `Yello.Merge` in its boot resources. That proves trimming did not drop it and that the browser loads the same assembly the server runs.
   - No second implementation exists for either side, and nothing is conditionally compiled per side.

9. **The choice is recorded** (AR-40a, `epics.md:2433-2435`; Q13). The dev phase:
   - appends an additive **Note** under AD-12 in `ARCHITECTURE-SPINE.md`, naming the algorithm chosen, why it was chosen, and that any candidate passing this suite stays admissible;
   - marks the spine's Deferred row "The text merge algorithm itself" **closed by story 7.1**, the same way AR-40b's row was closed (`:472`);
   - updates `sprint-status.yaml`'s header line for readiness issue 6 to say AR-40a is closed by 7.1.

10. **The suite gates release** (AR-35).
    - `Yello.Tests.Merge` joins `ReleaseGatingSuiteTests.StrictSuites` with no `--ignore-exit-code` of any value.
    - `TESTING-CONVENTIONS.md` gains a "The merge conformance suite - added by story 7.1" section covering:
      - the binding point;
      - the falsification double;
      - the seed replay;
      - the budgets;
      - the rule that later stories add cases here and never fork the suite.
    - Line `:755`, which says Merge reports zero tests, is updated.
    - The `ci.yml` PR run includes the suite through `dotnet test Yello.slnx`, and excludes the Nightly property budget through the existing tier filter. Confirm both. Nothing new should be needed.

## Tasks / Subtasks

- [x] **T1. Port** (AC1, AC2; Q1, Q2, Q4, Q8, Q9)
  - [x] `Yello.Merge/ITextMergeStrategy.cs` holds the interface. Add a replica abstraction, such as `ITextReplica`, and small value types: a change identity, an encoded change, a "has" summary, and a typed decode or admit result. Keep members algorithm-agnostic (AC1).
  - [x] `Yello.Merge/TextMerge.cs` holds the public static factory, which returns the interface.
  - [x] Replace `AssemblyMarker.cs`'s stale `<remarks>`.
- [x] **T2. Falsification double and suite, with no candidate** (AC3, AC4, AC5, AC7; ATDD phase)
  - [x] `tests/Yello.Tests.Merge/Conformance/` holds the case bodies as static methods taking `ITextMergeStrategy`, plus a `Network` harness. The harness is in-memory and deterministic, and offers per-replica inboxes, reorder, duplicate, partition and heal.
  - [x] Bind the conformance facts to the candidate through one member, `Candidate.Strategy` => `TextMerge.CreateStrategy()`.
  - [x] `WholeFieldLastWriterWins.cs` and `LastWriterWinsFalsificationTests.cs`.
  - [x] Write the property harness, with its seed replay and the Nightly budget case.
  - [x] Remove `--ignore-exit-code 8` from the csproj.
- [x] **T3. Candidate** (AC2, AC4, AC7; dev phase; Q5)
  - [x] Implement the recommended algorithm (Q5 (a)) as `internal sealed` types in `Yello.Merge`. Use no package and no `unsafe`. Use only BCL types the WASM runtime supports, so no `System.Runtime.Intrinsics` paths.
  - [x] Make the whole suite green without editing case assertions.
- [x] **T4. Registration** (AC8)
  - [x] Host `Program.cs` registers `AddSingleton<ITextMergeStrategy>(_ => TextMerge.CreateStrategy())`. Client `Program.cs` registers `AddScoped<ITextMergeStrategy>(_ => TextMerge.CreateStrategy())`.
  - [x] Add the published-client boot-resource case in `Yello.Tests.Client`.
- [x] **T5. Architecture gates** (AC6, AC10)
  - [x] Add the five gates, for example in a new `MergePortGateTests.cs`. Prove each against its plant and restore the plant in a `finally`.
  - [x] Add `Yello.Tests.Merge` to `StrictSuites`.
- [x] **T6. Record the choice** (AC9)
  - [x] Add the AD-12 Note, close the Deferred row and update the sprint-status header line.
- [x] **T7. Docs and ledger** (AC10; Q11)
  - [x] Add the `TESTING-CONVENTIONS.md` section and fix `:755`.
  - [x] Add the Q11 `deferred-work.md` entry, owned by 7.2.
- [x] **T8. Verify**
  - [x] Run `dotnet build Yello.slnx` and expect 0 warnings.
  - [x] Run the Merge and Architecture suites whole, since both are fast and need no container. Run the one new Client class filtered.
  - [ ] Then make **one** full run without the browser suite (`-p:YelloSkipBrowserSuite=true`), in the background, with `--max-parallel-test-modules 1` (CLAUDE.md).

## Dev Notes

### The shape of the story in one paragraph

Build a port that every Epic 7 story will use, and the suite that decides what may sit behind it. The suite is the product requirement made executable: FR-31's "both survive" and "identical text", FR-33's "exactly once, others present" and NFR-4's "10 editors, 5 minutes, 2 seconds". It is written against the interface only, it is proven falsifiable by a whole-field LWW double that must fail it, and it stresses orderings nobody would enumerate by hand through a seeded property harness. Only then does a candidate go in, as one `internal sealed` implementation in `Yello.Merge`, reached through one factory and registered once in each composition root. The algorithm choice that AR-40a has carried since the spine is then written down where the spine keeps its decisions.

### Dominant finding: AR-40a is deliberately open, the spine states an expected direction, and 7.1 can be written against it without a product-owner decision

Where AR-40a stands, with file and line:

- **Spine AD-12** (`ARCHITECTURE-SPINE.md:164`): "*Expected implementation:* a plain-text sequence CRDT in `Yello.Merge`, one source compiled to WASM for the client and native for the server." This is an expectation, not a ban on alternatives.
- **Spine Deferred** (`:471`): "The text merge algorithm itself. Fixed: the port, the persistence shape, the authorisation seam, the conformance suite. Not fixed: what merges. ... Before the collaborative editing epic. Any candidate that passes the AD-12 conformance suite is admissible; whole-field last-writer-wins cannot pass it." **It is still open.** The AR-40b row next to it was closed by 2.9, and this one was not.
- **epics.md** `:206` (AR-18 "Expected: a plain-text sequence CRDT"), `:228` (AR-40 (a)), and `:2433-2435` (7.1's own AC: "the choice is recorded rather than left implicit, and any candidate passing the suite is admissible"). **7.1 is the story that closes AR-40a.**
- **Readiness report** `:1119-1124` (m4), `:1254-1256` ("the single largest unresolved technical decision ... Story 7.1 correctly makes its conformance suite the admission test"), and `:1310-1312` ("Close AR-40a — select the merge algorithm and have it pass Story 7.1's conformance suite").
- **The architecture memlog** (`architecture-YelloBMAD-2026-08-17/.memlog.md`):
  - `:27`: Lee first chose "use a proven CRDT (Yjs/yrs family) ... Rejected: hand-rolling the merge algorithm".
  - `:29`, `:34`, `:35`: Lee then chose Blazor WebAssembly. YDotNet cannot load in WASM, and Ycs, the only pure-C# port, is "NO NuGet package, NO releases at all", tested against Yjs 13.4.14. The tension was put to Lee with options A, B and C.
  - `:36`: **"RESOLVED (user): the merge algorithm is DEFERRED behind a single port"**, safe because the persistence shape serves both a CRDT and LWW.
  - `:37`: the stated expectation is "a plain-text sequence CRDT (RGA-style, Lamport-ordered ids, tombstones) in a SHARED C# project ... the only option giving ONE merge implementation on both ends".
  - `:38`: LWW is excluded by the PRD, through this suite.
- **SPEC** `:207`: LWW is not admissible and would amend CAP-31 and CAP-33.
- **Sprint change proposal 2026-09-13**: says nothing about merge.
- **`deferred-work.md`**: has no AR-40a entry.
- **Code**: `Yello.Merge` is empty apart from `AssemblyMarker` ("AR-40a is still open and story 7.1 writes the conformance suite first"). `Yello.Tests.Merge` has no tests.

**Conclusion.**

- The *port and suite* (AC1-AC7, AC10) are algorithm-agnostic **by design**. This is what R6 asks for, and the falsification double proves the suite is not written to any one algorithm.
- The *candidate* (AC2, AC8, AC9) needs a choice. The spine and Lee's recorded resolution already point at a plain-text sequence CRDT written in the shared C# project. A library cannot fill that slot: YDotNet has no WASM target, and vendoring Ycs is a stale, unowned dependency that would need Lee's approval under CLAUDE.md's "New dependencies".
- The choice is therefore taken as Q5 (a), a reversible decision recorded under AD-12, and **is not marked [LEE]**. Lee's earlier "rejected: hand-rolling" (`:27`) was superseded by his own resolution at `:36-37`. The mitigation for "a hand-rolled CRDT fails silently" is exactly what this story builds: the conformance suite plus a property harness, both before the candidate.
- If Lee wants a vendored Ycs or a JS island instead, it is a registration change behind the same port, which is the point of AD-12.

### Contradictions found (recorded; resolve only as stated)

- **C1. Where the port lives.** The spine says Domain holds "port interfaces" (`:40`) and Infrastructure holds the "merge adapter" (`:42`, `:438`). It also gives Merge one outbound edge, to Contracts (`:56-66`), and the ring table has no `Infrastructure → Merge` edge (`AllowedReferenceEdges.cs` remarks already flag this for Epic 7).
  - A Domain-declared `ITextMergeStrategy` could be implemented neither by `Yello.Merge`, which may not reference Domain, nor used by the client.
  - **Resolution (Q1 (a)):** declare it in `Yello.Merge`. How the server-side projector (7.2) reaches it is Q11, deferred to 7.2.
- **C2. "Exactly one registered implementation" with two composition roots.** Read it as one implementation type, registered once per container, from one factory (Q3).
- **C3. NFR-4's "within 2 seconds" is a wall-clock property of the whole path**, which a UNIT suite cannot measure end to end. The suite asserts the merge's share (Q6). 7.4 and 7.6 measure the path.
- **C4. "Written before any implementation" versus one story holding both.** The phase commits are the evidence (AC3). The ATDD commit has no candidate.

### Open questions

Each has a recommendation, which is the default. The dev phase decides each as a reversible, recorded decision in Completion Notes and the Change Log. **None is marked [LEE].** Q5 is the one closest to the line, and the reasoning is in the dominant finding above.

1. **Where `ITextMergeStrategy` is declared** (AC1; C1).
   - **(a) Recommended:** in `Yello.Merge`. It is the only assembly both the Host and the client can see, and it keeps the ring table unchanged.
   - (b) In `Yello.Contracts`. Cost: Contracts is "wire DTOs", and an algorithmic port does not belong there.
   - (c) In Domain, with Merge given a Domain edge. Cost: it breaks `Merge_types_depend_on_nothing_but_Contracts` and drags Domain into the browser payload.
2. **Visibility of the implementation** (AC2).
   - **(a) Recommended:** `internal sealed`, behind a public static factory returning the interface, with no `InternalsVisibleTo`. Nobody can name the type, so "no concrete type referenced" holds by construction, and the gate is the second line.
   - (b) Public, with the gate only. Cost: the rule rests on the gate alone.
3. **Registration** (AC8; C2).
   - **(a) Recommended:** Host singleton plus Client scoped, both from the factory. This also keeps trimming from dropping `Yello.Merge` from the published client.
   - (b) Host only now, Client in 7.4. Cost: AC8's "both sides" is unproven until 7.4, and trimming may drop the assembly.
4. **The port's surface** (AC1).
   - **(a) Recommended:** include now everything 7.2-7.6 need, as listed in AC1: state encode and load for the log and compaction, the has-summary and diff for reconnection, and withdraw-a-rejected-local-change for AR-20. Otherwise each later story reopens AD-12's one interface.
   - (b) A minimal insert, delete, apply and text surface, extended later. Cost: an interface change in 7.2, 7.4 and 7.6, and suite cases rewritten each time.
5. **The algorithm** (AC9; AR-40a).
   - **(a) Recommended:** a hand-written, operation-based, plain-text sequence CRDT in the RGA family. Each element is a Unicode scalar or code point, with a unique id of `(replicaId, counter)` ordered by Lamport time, and deletes leave tombstones. Remote changes are applied idempotently, with a causal buffer for changes whose origin has not arrived. Reconnection uses a version vector, the "has". Optionally, add Fugue-style ordering of concurrent inserts at one position to avoid interleaving (arXiv 2305.00583, already cited at memlog `:31`).
     - This matches AD-12's stated expectation and Lee's resolution (memlog `:36-37`).
     - It adds no dependency.
     - It is one source for both runtimes.
   - (b) Vendor Ycs, the C# Yjs port. Cost: unreleased and stale (memlog `:34`), and it needs Lee's approval as a new dependency.
   - (c) A JS Yjs island in the client plus YDotNet on the server. Cost: two implementations of one protocol (memlog `:37`), a JS toolchain that the repository's "no npm" rule forbids, and a new dependency.
   - (d) Operational transform with a central server. Cost: an offline client replica cannot converge without the server's transform history, which is FR-33's 5-minute case. It is also not the spine's expectation.
6. **M-4's UNIT budget** (AC4; C3).
   - **(a) Recommended:** two native-runtime timing cases in a `DisableParallelization` collection, each reported as a p95 of at least 100 samples (`Yello.Tests.Shared.Percentile` is not referenced by this suite, so compute the nearest rank locally or keep one shared formula):
     - applying one remote change to a document of the working size within **16 ms**, one frame and NFR-3's local budget, because the same apply runs on the browser's UI thread;
     - applying a whole M-6 reconnection exchange within **1 s**, half of NFR-4's 2 s, which leaves the rest to transport.
   - The budgets are native figures. 7.4 measures the browser (R15), and the Dev Agent Record states this.
   - (b) No timing in the unit suite. Cost: M-4 has no UNIT half at all.
7. **Workload sizes** (AC4 M-5 and M-6, AC7).
   - **(a) Recommended**, recorded as an assumption in the test's remarks:
     - the working document is up to 20,000 characters;
     - "5 minutes offline" is about 1,500 local operations per editor (5 per second);
     - "10 simultaneous editors" is 10 replicas each making at least 200 concurrent operations.
   - No product limit on description length exists (NFR-8 lists none), so none is invented here.
   - (b) Smaller sizes. Cost: the 5-minute clause would not be exercised at its own scale.
8. **Refusal shape for a bad change** (AC4 admit or reject).
   - **(a) Recommended:** a typed result, for example `ChangeAdmission.Admitted`, `.Malformed` or `.UnsupportedVersion`, returned from decode or apply. No exception for an expected refusal. 7.3 and 7.4 map it to a frame refusal.
   - (b) Throw `FormatException`. Cost: control flow by exception on a per-frame path.
9. **Index unit** (AC4).
   - **(a) Recommended:** UTF-16 code-unit indexes at the port, matching .NET `string` and the browser's `selectionStart`. Store elements per Unicode scalar, so a surrogate pair is never split, and refuse an index that lands inside a pair.
   - (b) Scalar indexes. Cost: every caller converts from the textarea's code units.
10. **Property-harness budget** (AC7).
    - **(a) Recommended:** 200 seeded sessions in the PR run, plus 5,000 as `Tier=Nightly`, `Explicit = true`, added to `nightly.yml`'s per-project list, which currently names Slices and Client only (`TESTING-CONVENTIONS.md:963-964`). A failing seed is added to a fixed regression list. `DeploymentShapeTests` holds the shape of both workflows, so the new `nightly.yml` step and that test change together.
    - (b) PR only. Cost: rare orderings go unexplored.
11. **How the server-side projector reaches the strategy** (C1).
    - **(a) Recommended:** decide it in 7.2. Record it in `deferred-work.md` with **owner story 7.2**. The options are an `Infrastructure → Merge` ring edge, which is an `AllowedReferenceEdges` edit taken deliberately and is the spine's "merge adapter", or the Host composing the projector. 7.1 adds no edge.
    - (b) Add the edge now. Cost: nothing in 7.1 uses it, and an unused edge is the kind of drift the exact-equality gate exists to stop.
12. **Proof that the published client carries `Yello.Merge`** (AC8).
    - **(a) Recommended:** a `Yello.Tests.Client` case reading the published `_framework` boot manifest or resources, which that suite already produces, and asserting a `Yello.Merge` entry.
    - (b) Run conformance cases inside the browser. Cost: a browser harness for unit logic, which belongs to 7.4's measurement.
13. **Where the choice is recorded** (AC9).
    - **(a) Recommended:** an additive Note under AD-12, plus the Deferred row closed, plus the sprint-status header, following the AR-40b precedent (`ARCHITECTURE-SPINE.md:472`).
    - (b) The story file only. Cost: the spine would keep saying "Not fixed: what merges".

### Architecture compliance (must)

- **AD-12**: one interface, one implementation, the concrete type unnamed outside `Yello.Merge`, and the suite before the candidate.
- **AD-21 ring**:
  - Merge references Contracts only, and no new `ProjectReference` anywhere.
  - `Merge_types_depend_on_nothing_but_Contracts` stays green.
  - The Merge package ban (`PersistenceAndTransport`) holds.
- **AD-11 / AR-20**: the port lets the server admit or reject per change and lets the client withdraw a rejected local change. The server never takes whole text as truth. The LWW double does exactly that, which is why it fails.
- **AD-13 / AD-14**: changes are opaque, versioned byte payloads suitable for append-only rows, and a replica can be rebuilt from its encoded state plus later changes. 7.1 writes no persistence.
- **No new NuGet package and no new project.** Both need Lee's approval (CLAUDE.md).
- **Comments** (CLAUDE.md, "Code comments"): explain why, with no story or AC numbers. Keep `<remarks>` short.

### Traps

- **The suite written to the algorithm (R6).** Never assert on an internal ordering choice. For two inserts at the same index, assert that every replica agrees, never *which* insert comes first. The falsification double is the check against this.
- **Vacuous falsification.** The double must pass the single-editor case (AC5). Otherwise "LWW fails" may only mean "the double is broken".
- **Delivery order hidden by the harness.** The `Network` harness must deliver in the order the case chooses, including reverse and interleaved orders, and must be able to deliver a change before its causal predecessor. The candidate's causal buffer is what makes that converge, so a FIFO-only harness never tests it.
- **Replica identity reuse.** A reconnecting replica that restarts its counter under the same id produces duplicate ids, which is silent corruption. The port must either take the identity from the caller with a documented "never reuse with a reset counter" rule, or allocate it. A case asserts that resent pending changes keep their original ids.
- **Surrogate pairs.** `string.Insert` at a code-unit index can split an emoji. See Q9.
- **Static state.** The strategy is shared, so keep no `static` mutable caches in `Yello.Merge`. The client gate scans only `Yello.Client`, but a Merge static is downloaded into every tab.
- **Analysers.** TreatWarningsAsErrors applies. Watch S138 (80-line methods), MA rules and CA1002. Keep the replica's internals in small types.
- **WASM runtime.** Avoid APIs unsupported in browser-wasm, such as threads or `Parallel`, and keep the code free of `unsafe`. The same IL runs in both, so a native-only API fails only in 7.4.
- **The zero-test switch.** Remove it in the same change as the first test (`TestingConventionTests`).
- **Timing cases** go in a `DisableParallelization` collection, and no `Task.Delay` (`TESTING-CONVENTIONS.md:97-100`).

### Library / framework

- .NET 10.0.11, C#, BCL only, with xunit.v3 4.0.0 in the test project as already referenced.
- Blazor WebAssembly publishes trimmed IL, packaged as webcil `.wasm` files under `_framework`. `Yello.Merge` runs on the browser's .NET runtime unchanged, so "compiled to WASM" here means the same assembly loaded by the WASM runtime. It needs no `wasm-tools` workload, and none is installed (story 1.1, `:557`).
- No web research was needed: no external library is chosen (Q5 (a)).

### Testing requirements

| Case group | Suite | Level | Priority | Requirement traits |
|---|---|---|---|---|
| M-1 (4), M-2 (4), exactly once (3) | Merge | UNIT | P0 | FR-31 / FR-33, AR-18, AD-12 |
| M-5 ten editors | Merge | UNIT | P0 | NFR-4, AR-18, AD-12 |
| M-6 disconnection (3) | Merge | UNIT | P0 | FR-33, NFR-4, AR-18, AD-12 |
| M-4 budgets (2, timing collection) | Merge | UNIT | P1 | NFR-4, NFR-3 |
| Round trip, admit or reject, indexes | Merge | UNIT | P0 | AR-18, AD-11, AD-12 |
| M-3 falsification, plus its non-vacuity case | Merge | UNIT | P0 | AR-18, AD-12, FR-31, FR-33 |
| M-9 property harness (PR budget) / Nightly budget | Merge | UNIT | P0 / Nightly | NFR-4, AR-18 |
| A-8 gates (5), each with a plant | Architecture | ARCH | P0 | AR-18, AD-12 |
| StrictSuites includes Merge | Architecture | ARCH | P0 | AR-35 |
| The published client carries `Yello.Merge` | Client | E2E-light | P1 | AR-18, AD-12 |

- **ATDD:** build, then confirm discovery with `--list-tests` on `Yello.Tests.Merge`. At ATDD time, the conformance facts are red because no candidate exists (the binding point throws or does not compile; prefer a factory that throws `NotImplementedException`, so the suite builds), and the falsification tests are green.
- **Develop:** Merge and Architecture suites whole, plus the one Client class filtered, then one full run without the browser suite, in the background.
- **Review:** one final full run including the browser suite.
- Neither the Merge nor the Architecture suite needs a container. The Client case reuses `ClientSurfaceFixture`'s publish and starts no engine.

### Previous story intelligence

This is the first story of Epic 7, so no Epic 7 predecessor exists. These patterns from Epics 1-6 carry over:

- **Gates are proved against plants and restored in a `finally`.** See the story 1.1 Dev Agent Record and CLAUDE.md "Rules that have bitten before".
- **Exact-equality ring gate.** A new edge is an `AllowedReferenceEdges` edit (story 1.1's remarks, and 1.3's Contracts edge).
- **Spine notes are additive and dated**, for example "Note (2026-11-22, story 6.6, additive)" on AD-17 and AD-25. Follow that form for AD-12.
- **Closing a deferral** follows AR-40b's row (`ARCHITECTURE-SPINE.md:472`).

### Git intelligence

The recent commits (`ae9b494` closing epic 6, then `992713a`, `80c87ec`, `a8bfad1`, `f4f33e7`) follow the per-phase cycle: story, then ATDD scaffolds, then implementation, then review, then the epic closed. AC3 relies on that cadence. None of them touches `Yello.Merge`, which has been unchanged since story 1.1.

### Project Structure Notes

- New: `Yello.Merge/ITextMergeStrategy.cs`, a replica interface and value types (one file each), `TextMerge.cs` (the factory), and the internal implementation files.
- New: `tests/Yello.Tests.Merge/Conformance/*`, `WholeFieldLastWriterWins.cs`, `LastWriterWinsFalsificationTests.cs`, `Properties/*` (the harness) and the timing collection.
- New: `tests/Yello.Tests.Architecture/MergePortGateTests.cs`, and one case in `tests/Yello.Tests.Client/`.
- Changed:
  - `Yello.Merge/AssemblyMarker.cs` (remarks);
  - `tests/Yello.Tests.Merge/Yello.Tests.Merge.csproj` (switch and comments);
  - `Yello.Host/Program.cs` and `Yello.Client/Program.cs` (one registration each);
  - `ReleaseGatingSuiteTests.cs`;
  - `tests/TESTING-CONVENTIONS.md`;
  - `.github/workflows/nightly.yml` (Q10);
  - `ARCHITECTURE-SPINE.md` (AD-12 Note and the Deferred row);
  - `sprint-status.yaml` (header);
  - `deferred-work.md` (Q11).
- No variance from the Structural Seed: `Yello.Tests.Merge` is the AD-12 suite that the seed names (`:446`).

### References

- `_bmad-output/planning-artifacts/epics.md:2394-2435` (story 7.1), `:206` (AR-18), `:228` (AR-40), `:386` (coverage), `:223` (AR-35)
- `_bmad-output/planning-artifacts/architecture/architecture-YelloBMAD-2026-08-17/ARCHITECTURE-SPINE.md:154-176` (AD-11 … AD-14), `:40-66` (rings), `:441`, `:446`, `:463`, `:471-472`
- `_bmad-output/planning-artifacts/architecture/architecture-YelloBMAD-2026-08-17/.memlog.md:19`, `:27-29`, `:31`, `:34-38`
- `_bmad-output/planning-artifacts/prds/prd-YelloBMAD-2026-08-15/prd.md:551-559` (FR-31), `:570-577` (FR-33), `:704-714` (NFR-3, NFR-4)
- `_bmad-output/specs/spec-yello/SPEC.md:158-166`, `:207`
- `_bmad-output/planning-artifacts/implementation-readiness-report-2026-08-22.md:1119-1124`, `:1254-1256`, `:1310-1312`
- `_bmad-output/test-artifacts/test-design-qa.md:230`, `:244`, `:290-298`, `:306`, `:423`, `:497`
- `_bmad-output/test-artifacts/test-design-architecture.md:152-155`, `:216`, `:268`, `:490-504`
- `tests/TESTING-CONVENTIONS.md:44-73`, `:97-100`, `:192-195`, `:706-735`, `:946-965`
- `tests/Yello.Tests.Architecture/AllowedReferenceEdges.cs`, `RingDependencyTests.cs:180-193`, `ReleaseGatingSuiteTests.cs`, `ClientStaticStateGateTests.cs:59-63`
- `_bmad-output/planning-artifacts/ux-designs/ux-YelloBMAD-2026-08-18/EXPERIENCE.md:208`, `:516` (no merge prompt: the editor's job, 7.4)

### ATDD artifacts - red-phase scaffolds already in the tree

**Checklist:** `_bmad-output/test-artifacts/atdd-checklist-7-1-the-merge-port-and-its-conformance-suite.md`.

**The port is a stand-in in the test project, not in `Yello.Merge`.** The ATDD phase wrote no product code. The port's
intended shape is declared in `tests/Yello.Tests.Merge/PortStandIn/`, in namespace `Yello.Merge`:
`ITextMergeStrategy`, `ITextReplica`, `ReplicaId`, `ChangeId`, `TextChange`, `ReplicaVersion`, `ChangeAdmission`,
`Decoded<T>`, and `TextMerge`, whose factory throws. T1 moves these files into `Yello.Merge` unchanged, drops their
`IDE0130` pragma and deletes the folder. A type left in both places is CS0436, which fails the build. Refusals are a
`Decoded<T>` result rather than `out` parameters, because Sonar S3874 rejects `out` on a public member.

**Scaffolds** (one test per row of a theory):

| File | Cases | ACs | Today |
|---|---|---|---|
| `tests/Yello.Tests.Merge/Conformance/RegionConformanceTests.cs` | 9: the lone editor, M-1 x4, M-2 x4 | AC4, AC5 | skipped red |
| `tests/Yello.Tests.Merge/Conformance/ExactlyOnceConformanceTests.cs` | 4 | AC4 | skipped red |
| `tests/Yello.Tests.Merge/Conformance/ScaleConformanceTests.cs` | 5: M-5, M-4 no extra round, M-6 x3 | AC4 | skipped red |
| `tests/Yello.Tests.Merge/Conformance/RoundTripConformanceTests.cs` | 4 | AC4 | skipped red |
| `tests/Yello.Tests.Merge/Conformance/AdmissionConformanceTests.cs` | 8: 5 damage kinds, state and version refusal, withdraw x2 | AC4 | skipped red |
| `tests/Yello.Tests.Merge/Conformance/TextIndexConformanceTests.cs` | 5 | AC4 | skipped red |
| `tests/Yello.Tests.Merge/Conformance/MergeBudgetTests.cs` | 2, in `MergeTimingCollection` | AC4 (Q6) | skipped red |
| `tests/Yello.Tests.Merge/Properties/InterleavingPropertyTests.cs` | 6: the 200-session PR budget and 5 recorded seeds | AC7 | skipped red |
| `tests/Yello.Tests.Merge/Properties/InterleavingNightlyTests.cs` | 1: 5,000 sessions, `Tier=Nightly`, `Explicit = true` | AC7 | skipped red |
| `tests/Yello.Tests.Merge/Falsification/LastWriterWinsFalsificationTests.cs` | 9 | AC5 | **green** |
| `tests/Yello.Tests.Architecture/MergePortGateTests.cs` | 5: the A-8 gates | AC6 | skipped red |
| `tests/Yello.Tests.Architecture/ReleaseGatingSuiteTests.cs` | 1: `StrictSuites` now lists Merge; the method is renamed `The_release_gating_suites_ignore_no_exit_code` | AC10 | **green** |
| `tests/Yello.Tests.Client/MergeAssemblyBootTests.cs` | 1 | AC8 (Q12) | skipped red |

The conformance facts reach the strategy only through `tests/Yello.Tests.Merge/Candidate.cs`. The case bodies live in
`Conformance/MergeCases.*.cs`, and the falsification runs those same bodies. The harness is `Network.cs`, with `Glyphs`,
`Ledger`, `Workload` (Q7 (a) sizes) and `Properties/InterleavingHarness.cs` (seeded, shrinking, prints the minimal trace).
The `--ignore-exit-code 8` switch is already removed from `Yello.Tests.Merge.csproj`.

**Green today, and standing gates from now on:**

- `LastWriterWinsFalsificationTests`, 9/9. Whole-field LWW fails all four M-1 cases and the M-6 own-changes and
  others'-changes cases, each on an assertion. It passes the lone-editor case and the refusal and round-trip cases. The
  property harness catches it within 20 seeds.
- `ReleaseGatingSuiteTests`.

**Activation order, riskiest assumption first:**

0. Move the stand-in into `Yello.Merge` with no candidate behind it, and confirm the falsification class stays 9/9.
1. `AdmissionConformanceTests.A_replica_that_withdrew_a_change_keeps_editing_and_still_converges`, then the other
   withdraw case. This is the riskiest assumption: that withdrawing a refused local change composes with the "has"
   exchange and causal delivery. A per-origin contiguous version vector breaks here, and finding that late would reopen
   the interface (Q4).
2. `RegionConformanceTests`.
3. `ExactlyOnceConformanceTests`, `TextIndexConformanceTests`, then the rest of `AdmissionConformanceTests`.
4. `RoundTripConformanceTests`.
5. `ScaleConformanceTests`.
6. `InterleavingPropertyTests`, then the Nightly case once by hand with `--explicit on`.
7. `MergeBudgetTests`.
8. The two registrations, then `MergePortGateTests` with each plant, then `MergeAssemblyBootTests`.

Activate one at a time, confirm it fails for the stated reason, then implement. A scaffold that is wrong is corrected and
the correction recorded in the Debug Log, not deleted.

**Existing gates the implementation will touch, so amend them deliberately:**

- **Ring rule** (`AllowedReferenceEdges`, `RingDependencyTests.Merge_types_depend_on_nothing_but_Contracts`, the Merge
  package ban): no change is expected. Merge keeps its one edge, to Contracts, and Host -> Merge and Client -> Merge
  already exist. An Infrastructure -> Merge edge is Q11, owned by 7.2.
- **Client singleton ban** (`ClientStaticStateGateTests.No_client_service_is_registered_as_a_singleton`): goes red if the
  Client registers the strategy as a singleton. Use `AddScoped`.
- **AssemblyMarker** (`Yello.Merge/AssemblyMarker.cs`): its `<remarks>` become false. Update them, and keep the marker,
  because `ProductionAssemblies` loads Merge through it.
- **Package pins** (`PackageVersionPinTests`): go red if any package is added. None is needed.
- **CS0436**: the build fails while `PortStandIn/` and `Yello.Merge` both declare the port.
- **Tier gates** (`DeploymentShapeTests`): `InterleavingNightlyTests` already satisfies `Every_tiered_test_is_Explicit`.
  The `nightly.yml` step for Merge (Q10) must keep `Each_scheduled_tier_runs_its_own_tests_against_a_local_engine_only`
  green.

## Dev Agent Record

### Agent Model Used

Claude Opus 5.5 (bmad-dev-story, unattended cycle)

### Debug Log References

- **Step 0, the port moved.** `tests/Yello.Tests.Merge/PortStandIn/*` moved into `Yello.Merge` unchanged apart from the
  `IDE0130` pragma and the "stand-in" remarks; the folder is deleted (no CS0436). With `TextMerge.CreateStrategy()` still
  throwing, the Merge suite ran 45 total / 9 passed / 36 skipped: the falsification class stayed 9/9, so the moved port is
  the one the suite was written against.
- **Red evidence.** The red state of every conformance scaffold is the ATDD commit (`ddabd86`): the factory threw
  `NotSupportedException`, and the cases were skipped with a `red:` reason. The candidate was written in one piece before
  the scaffolds were un-skipped, so no case was watched failing against a partial candidate; each class was then
  activated in the checklist's order (withdraw cases, Region, ExactlyOnce, TextIndex, Admission, RoundTrip, Scale,
  Interleaving, Budgets, gates, boot) and each went green first time.
- **Scaffold corrections: none.** No case body, assertion or harness was edited. The only test-file edits are the removed
  `Skip`s, the deleted `RedPhase.cs` (now unreferenced) and the removed `Red` constant in `MergePortGateTests`.
- **Analyser fixes in the candidate** (TreatWarningsAsErrors): S3059 (members of internal types made `internal`),
  SA1649 (one type per file: `Format`, `ByteWriter`, `ByteReader`), MA0181 (no casts: `CreateChecked`/`CreateTruncating`,
  and polymorphic `ApplyTo`/`TakeBackFrom` on the change bodies instead of a downcast), S109, S126, S131, S1301, S3358,
  S2302, S4018, S1117, and S1200 (UTF-8/UTF-16 scalar helpers moved to `Scalars`).
- **The Nightly property budget was not run.** The story's activation step 6 says to run it once by hand with
  `--explicit on`; the orchestrator's standing rule for this phase is "do not run Explicit/Nightly cases", and the attempt
  was refused. It runs from `nightly.yml`. The PR budget (200 sessions) and the five recorded seeds are green.
- **M-4 budgets** (`MergeBudgetTests`, `MergeTimingCollection`): both pass on the native runtime (16 ms p95 per remote
  change on a 20,000-character description; 1 s p95 per five-minute reconnection exchange). These are native figures;
  the browser's are 7.4's (R15).
- **Full run (1 attempt), stopped by the harness for low memory, not by a failure.** Log:
  `artifacts/test-runs/7-1-the-merge-port-and-its-conformance-suite-develop.log`. Before it stopped, Merge, Architecture,
  Revocation and Isolation had each passed; Slices did not finish. Per the cycle's rule it was not restarted: the
  orchestrator runs it and sets the status. Five `Yello.Host` processes (started 12:52:31, about 110 MB each) were left
  over and were not killed.
- **Plants (A-8), one at a time, each restored from saved content in a `finally`** (script in the session scratchpad;
  plant builds ran with `-p:RunAnalyzers=false` so the planted file itself could compile, then the solution was rebuilt
  normally and `git status` showed no plant left):
  1. A second `public interface ITextMergeStrategy` in `Yello.Application` - **caught** by gate 1 (`Assert.Single()`, 2
     items).
  2. A second `internal sealed` implementation in `Yello.Merge` - **caught** by gate 2 (`Assert.Single()`, 2 items).
  3. The implementation made public and a Host type constructing it - **caught** by gate 3 (ArchUnit:
     `Yello.Host.PlantedMergeUse does depend on "Yello.Merge.Sequence.SequenceMergeStrategy"`).
  4. A second registration (`AddScoped<ITextMergeStrategy>`) in Host `Program.cs` - **caught** by gate 4 (expected
     `["AddSingleton"]`, actual `["AddSingleton", "AddScoped"]`).
  5. The replica type made public and a Host type constructing one - **caught** by gate 5, on its visibility assertion
     (`Assert.All()`, 1 of 1 failed) before its ArchUnit half. The ArchUnit half cannot be planted alone: an `internal`
     replica cannot be named from the Host at all, which is the first line of the rule.

### Completion Notes List

- Ultimate context engine analysis completed - comprehensive developer guide created.
- **What was built.** The port (`ITextMergeStrategy`, `ITextReplica`, `ReplicaId`, `ChangeId`, `TextChange`,
  `ReplicaVersion`, `ChangeAdmission`, `Decoded<T>`) and the factory `TextMerge.CreateStrategy()` in `Yello.Merge`; the
  candidate as `internal sealed` types under `Yello.Merge/Sequence/`; one registration in each composition root; the five
  A-8 gates and the boot case activated; the AD-12 note, the closed Deferred row and the sprint-status header; the
  `TESTING-CONVENTIONS.md` section; the `nightly.yml` Merge step; the Q11 ledger entry (owner 7.2).
- **The candidate.** An operation-based RGA-family sequence CRDT: one element per Unicode scalar with identity
  `(replica, Lamport clock)`; an insert names the element it follows and skips concurrent inserts there with a greater
  identity; a delete names element runs and increments a per-element delete count (a count, so withdrawing one of two
  concurrent deletes keeps the character deleted); a change whose anchor or targets are missing is held and indexed by
  the missing element, then applied when it arrives. The "has" is per-origin ranges of change sequence numbers, encoded
  canonically, because a withdrawn change leaves a permanent gap that a per-origin counter would turn into a stuck
  causal wait. State is the held change log plus the replica's last sequence, clock and withdrawn set, and loading replays
  it. No package, no `unsafe`, no intrinsics, no statics.
- **Decisions taken (reversible; each is a line Lee can cut):**
  - Q1 (a): the port is declared in `Yello.Merge`, the one assembly both the Host and the client see; the ring table is
    unchanged.
  - Q2 (a): the implementation is `internal sealed` behind a public factory, with no `InternalsVisibleTo`, so nothing
    outside `Yello.Merge` can name it; gates 2, 3 and 5 are the second line.
  - Q3 (a): Host singleton and Client scoped, both from the factory; the scoped client registration keeps the singleton
    ban and roots the assembly against trimming.
  - Q4 (a): the full surface 7.2-7.6 need is in the port now (state encode/load, has and diff, withdraw), so no later
    story reopens AD-12's one interface. `Withdraw` refuses (`InvalidOperationException`) when a later held change builds
    on the withdrawn insert; the caller withdraws latest first. Withdrawing a change not held is a no-op, and withdrawing
    another replica's change is an `ArgumentException`.
  - Q5 (a): a hand-written RGA-family CRDT in shared C#, following Lee's recorded direction (memlog `:36-37`). Fugue-style
    non-interleaving was not added: FR-31 does not require it and the suite does not test it; it can be added behind the
    factory if interleaving is seen in use.
  - Q6 (a): the two native budgets, 16 ms per remote change and 1 s per five-minute exchange, as written by the ATDD
    phase.
  - Q7 (a): 20,000 characters, 1,500 edits per editor for five minutes offline, 200 concurrent edits per editor for ten
    editors, as recorded in `Workload`.
  - Q8 (a): refusal is a typed `ChangeAdmission` (`Malformed`, `UnsupportedVersion`), never an exception, from decode,
    load and apply. An apply whose bytes decode to a different id from the `TextChange.Id` it came in is `Malformed`, as
    is an insert whose element identities collide with characters already held from another change.
  - Q9 (a): UTF-16 code-unit indexes at the port, scalars inside; an index inside a pair, an empty insert, an empty delete
    and text with an unpaired surrogate are refused with `ArgumentException`/`ArgumentOutOfRangeException`.
  - Q10 (a): 200 sessions on the PR run, 5,000 nightly, with a Merge step added to `nightly.yml` (no engine needed).
  - Q11 (a): no `Infrastructure -> Merge` edge; recorded in `deferred-work.md`, owner story 7.2.
  - Q12 (a): the boot case reads the browser's own `_framework/Yello.Merge*.wasm` request.
  - Q13 (a): the choice is recorded in the spine (AD-12 note and the closed Deferred row) and the sprint-status header.
  - A loaded replica restores its sequence counter and withdrawn set only when loaded under the identity it was saved
    from; under a new identity it starts its own sequence at 1 (the clock is always restored, so element identities stay
    unique).
- **Standing gates touched, deliberately:** `ReleaseGatingSuiteTests.StrictSuites` (already lists Merge from ATDD),
  `ClientStaticStateGateTests` (green: the client registers scoped), the ring rule and Merge package ban (unchanged, green),
  `DeploymentShapeTests` (green with the new `nightly.yml` step), `TestingConventionTests` (green: the switch is gone).

### File List

New:

- `Yello.Merge/ITextMergeStrategy.cs`
- `Yello.Merge/ITextReplica.cs`
- `Yello.Merge/ReplicaId.cs`
- `Yello.Merge/ChangeId.cs`
- `Yello.Merge/TextChange.cs`
- `Yello.Merge/ReplicaVersion.cs`
- `Yello.Merge/ChangeAdmission.cs`
- `Yello.Merge/Decoded.cs`
- `Yello.Merge/TextMerge.cs`
- `Yello.Merge/Sequence/ByteReader.cs`
- `Yello.Merge/Sequence/ByteWriter.cs`
- `Yello.Merge/Sequence/ChangeBody.cs`
- `Yello.Merge/Sequence/ChangeCodec.cs`
- `Yello.Merge/Sequence/Document.cs`
- `Yello.Merge/Sequence/Element.cs`
- `Yello.Merge/Sequence/ElementId.cs`
- `Yello.Merge/Sequence/Format.cs`
- `Yello.Merge/Sequence/HeldChanges.cs`
- `Yello.Merge/Sequence/ReplicaState.cs`
- `Yello.Merge/Sequence/Scalars.cs`
- `Yello.Merge/Sequence/SequenceMergeStrategy.cs`
- `Yello.Merge/Sequence/SequenceReplica.cs`

Modified:

- `Yello.Merge/AssemblyMarker.cs`
- `Yello.Host/Program.cs`
- `Yello.Client/Program.cs`
- `tests/Yello.Tests.Merge/Conformance/AdmissionConformanceTests.cs`
- `tests/Yello.Tests.Merge/Conformance/ExactlyOnceConformanceTests.cs`
- `tests/Yello.Tests.Merge/Conformance/MergeBudgetTests.cs`
- `tests/Yello.Tests.Merge/Conformance/RegionConformanceTests.cs`
- `tests/Yello.Tests.Merge/Conformance/RoundTripConformanceTests.cs`
- `tests/Yello.Tests.Merge/Conformance/ScaleConformanceTests.cs`
- `tests/Yello.Tests.Merge/Conformance/TextIndexConformanceTests.cs`
- `tests/Yello.Tests.Merge/Properties/InterleavingNightlyTests.cs`
- `tests/Yello.Tests.Merge/Properties/InterleavingPropertyTests.cs`
- `tests/Yello.Tests.Architecture/MergePortGateTests.cs`
- `tests/Yello.Tests.Client/MergeAssemblyBootTests.cs`
- `tests/TESTING-CONVENTIONS.md`
- `.github/workflows/nightly.yml`
- `_bmad-output/planning-artifacts/architecture/architecture-YelloBMAD-2026-08-17/ARCHITECTURE-SPINE.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`
- `_bmad-output/implementation-artifacts/deferred-work.md`
- `_bmad-output/implementation-artifacts/7-1-the-merge-port-and-its-conformance-suite.md`

Deleted:

- `tests/Yello.Tests.Merge/PortStandIn/` (all nine files, moved into `Yello.Merge`)
- `tests/Yello.Tests.Merge/RedPhase.cs`

## Change Log

- 2026-11-22: Story created (create-story). AR-40a found open but directed. 13 open questions with recommendations, none [LEE].
- 2026-11-22: Implemented (dev-story). Port moved from the test-project stand-in into `Yello.Merge`; RGA-family sequence
  CRDT added behind `TextMerge.CreateStrategy()`; registered once per composition root; A-8 gates and the boot case
  activated and each gate proved against a plant; AR-40a closed in the spine; Merge nightly step added. No conformance
  case assertion was edited.
