---
stepsCompleted: ['step-01-preflight-and-context', 'step-02-generation-mode', 'step-03-test-strategy', 'step-04c-aggregate', 'step-05-validate-and-complete']
lastStep: 'step-05-validate-and-complete'
lastSaved: '2026-11-22'
storyId: '7.1'
storyKey: '7-1-the-merge-port-and-its-conformance-suite'
storyFile: '_bmad-output/implementation-artifacts/7-1-the-merge-port-and-its-conformance-suite.md'
atddChecklistPath: '_bmad-output/test-artifacts/atdd-checklist-7-1-the-merge-port-and-its-conformance-suite.md'
generatedTestFiles:
  - tests/Yello.Tests.Merge/Conformance/RegionConformanceTests.cs
  - tests/Yello.Tests.Merge/Conformance/ExactlyOnceConformanceTests.cs
  - tests/Yello.Tests.Merge/Conformance/ScaleConformanceTests.cs
  - tests/Yello.Tests.Merge/Conformance/RoundTripConformanceTests.cs
  - tests/Yello.Tests.Merge/Conformance/AdmissionConformanceTests.cs
  - tests/Yello.Tests.Merge/Conformance/TextIndexConformanceTests.cs
  - tests/Yello.Tests.Merge/Conformance/MergeBudgetTests.cs
  - tests/Yello.Tests.Merge/Properties/InterleavingPropertyTests.cs
  - tests/Yello.Tests.Merge/Properties/InterleavingNightlyTests.cs
  - tests/Yello.Tests.Merge/Falsification/LastWriterWinsFalsificationTests.cs
  - tests/Yello.Tests.Architecture/MergePortGateTests.cs
  - tests/Yello.Tests.Architecture/ReleaseGatingSuiteTests.cs
  - tests/Yello.Tests.Client/MergeAssemblyBootTests.cs
inputDocuments:
  - _bmad-output/implementation-artifacts/7-1-the-merge-port-and-its-conformance-suite.md
  - _bmad-output/test-artifacts/test-design-qa.md
  - _bmad-output/test-artifacts/test-design-architecture.md
  - tests/TESTING-CONVENTIONS.md
  - _bmad/tea/config.yaml
  - .claude/skills/bmad-testarch-atdd/resources/knowledge/test-quality.md
  - .claude/skills/bmad-testarch-atdd/resources/knowledge/test-levels-framework.md
  - .claude/skills/bmad-testarch-atdd/resources/knowledge/test-priorities-matrix.md
  - .claude/skills/bmad-testarch-atdd/resources/knowledge/data-factories.md
  - .claude/skills/bmad-testarch-atdd/resources/knowledge/component-tdd.md
  - .claude/skills/bmad-testarch-atdd/resources/knowledge/test-healing-patterns.md
  - .claude/skills/bmad-testarch-atdd/resources/knowledge/ci-burn-in.md
---

# ATDD Checklist: Story 7.1 - The merge port and its conformance suite

## Step 1 - Preflight and context

- **Stack:** fullstack (.NET 10 `*.csproj`, Blazor WebAssembly client, Playwright for .NET). xunit.v3 4.0.0 on
  Microsoft.Testing.Platform. Playwright Utils and Pact are off (`_bmad/tea/config.yaml`).
- **Story:** ten ACs, all clear. AC1-AC2 are the port and its single hidden implementation; AC3-AC5 and AC7 the
  conformance suite, the LWW falsification and the property harness; AC6 the A-8 architecture gates; AC8 the two
  registrations and the published client; AC9 the recorded choice (docs only); AC10 release gating.
- **Framework:** `tests/Yello.Tests.Merge` exists with no tests and carries `--ignore-exit-code 8`.
  `tests/Yello.Tests.Architecture` holds the ring, client-singleton, release-gating, tier and zero-test gates.
  `tests/Yello.Tests.Client` publishes the client through `ClientSurfaceFixture`.
- **Knowledge fragments:** the core and backend fragments are TypeScript-flavoured; their rules (deterministic data,
  explicit assertions, one behaviour per test, no hard waits) are applied through `TESTING-CONVENTIONS.md`, which is the
  repository's authority.

## Step 2 - Generation mode

AI generation from the story, the test design and the conventions. No recording: the merge suite is UNIT, the gates are
ARCH, and the one browser case only observes the boot's network requests.

## Step 3 - Test strategy

| Group | Level | Project | Priority | Red today? |
|---|---|---|---|---|
| M-1 different regions (4) | UNIT | Merge | P0 | yes - no candidate |
| M-2 same region (4) | UNIT | Merge | P0 | yes |
| Exactly once (4: twice, after exchange, resend, identity kept) | UNIT | Merge | P0 | yes |
| M-5 ten editors + M-4 no extra round (2) | UNIT | Merge | P0 | yes |
| M-6 disconnection (3) | UNIT | Merge | P0 | yes |
| M-4 native budgets (2, timing collection) | UNIT | Merge | P1 | yes |
| Round trip (4) | UNIT | Merge | P0 | yes |
| Admit or reject (7) and withdraw (2) | UNIT | Merge | P0 | yes |
| Text indexes (6) | UNIT | Merge | P0 | yes |
| M-9 property harness: PR budget, seed replay | UNIT | Merge | P0 | yes |
| M-9 Nightly budget | UNIT | Merge | Nightly | yes (Explicit) |
| M-3 falsification: LWW fails, non-vacuity, harness catches LWW | UNIT | Merge | P0 | **no - green, a standing gate** |
| A-8 gates (5) | ARCH | Architecture | P0 | yes - no port in production |
| StrictSuites includes Merge | ARCH | Architecture | P0 | **no - green once the switch comes off** |
| Published client loads `Yello.Merge` | E2E-light | Client | P1 | yes - nothing references Merge yet |

Every red case asserts the expected behaviour and is skipped with `red: AC<n> - <why>`.

## Step 4 - Generation (sequential)

Execution mode resolved to **sequential**: the port shape, the harness and the case bodies are one design, so they were
written in one pass rather than split between an API worker and an E2E worker. The one browser case was written last.

### The port stand-in (no product code)

The orchestrator rule for this phase is "do not write product code", which overrides the story's "the ATDD phase commits
the port". So the port's intended shape is declared **in the test project**, in the production namespace `Yello.Merge`,
under `tests/Yello.Tests.Merge/PortStandIn/`:

| File | Declares |
|---|---|
| `ITextMergeStrategy.cs` | `CreateReplica(ReplicaId)`, `LoadReplica(ReplicaId, bytes)`, `DecodeChange(bytes)`, `DecodeVersion(bytes)` |
| `ITextReplica.cs` | `Id`, `Text`, `Version`, `Insert(index, text)`, `Delete(index, length)`, `Apply(change)`, `ChangesMissingFrom(version)`, `EncodeState()`, `Withdraw(changeId)` |
| `ReplicaId.cs`, `ChangeId.cs` | identity value types; `ChangeId(Origin, Sequence)` |
| `TextChange.cs`, `ReplicaVersion.cs` | opaque byte payloads with value equality; the first byte of every encoding is the format version |
| `ChangeAdmission.cs`, `Decoded.cs` | the typed refusal (Q8 (a)): `Admitted`, `Malformed`, `UnsupportedVersion`; `Decoded<T>(Admission, Value)` |
| `TextMerge.cs` | the factory, which throws `NotSupportedException` until replaced |

The dev phase **moves these files into `Yello.Merge` unchanged and deletes the folder** (dropping the `IDE0130` pragma,
which exists only because the folder does not match the namespace). Leaving both in place is CS0436, an error under
TreatWarningsAsErrors, so the swap cannot be half done. `Decoded<T>` replaced an `out`-parameter design because Sonar
S3874 rejects `out` on a public member, which the real port would also hit.

### Scaffolds

| File | Cases | ACs | State today |
|---|---|---|---|
| `Conformance/RegionConformanceTests.cs` | 9 (lone editor; M-1 x4; M-2 x4) | AC4, AC5 | skipped red |
| `Conformance/ExactlyOnceConformanceTests.cs` | 4 | AC4 (FR-33) | skipped red |
| `Conformance/ScaleConformanceTests.cs` | 5 (M-5; M-4 no extra round; M-6 x3) | AC4 | skipped red |
| `Conformance/RoundTripConformanceTests.cs` | 4 | AC4 | skipped red |
| `Conformance/AdmissionConformanceTests.cs` | 8 (theory of 5 damage kinds; state/version refusal; withdraw x2) | AC4 (AD-11, Q8) | skipped red |
| `Conformance/TextIndexConformanceTests.cs` | 5 | AC4 (Q9) | skipped red |
| `Conformance/MergeBudgetTests.cs` | 2 (`MergeTimingCollection`) | AC4 (M-4, Q6) | skipped red |
| `Properties/InterleavingPropertyTests.cs` | 6 (200-session PR budget; 5 recorded seeds) | AC7 (Q10) | skipped red |
| `Properties/InterleavingNightlyTests.cs` | 1 (`Tier=Nightly`, `Explicit = true`) | AC7 (Q10) | skipped red |
| `Falsification/LastWriterWinsFalsificationTests.cs` | 9 (6 cases LWW must fail; lone editor passes; refusal and round-trip cases pass; the harness catches LWW) | AC5 | **green - standing gate** |
| `Architecture/MergePortGateTests.cs` | 5 (A-8 gates 1-5) | AC6 | skipped red |
| `Architecture/ReleaseGatingSuiteTests.cs` | 1 (StrictSuites now includes Merge; method renamed `The_release_gating_suites_ignore_no_exit_code`) | AC10 | **green - standing gate** |
| `Client/MergeAssemblyBootTests.cs` | 1 | AC8 (Q12) | skipped red |

Support code (test project, not cases):

- `Candidate.cs`: the one binding point (AC3).
- `Network.cs`: in-memory inboxes; as-sent, reversed, shuffled and partial delivery; duplicates; isolate, and reconnect
  through the "has" exchange alone.
- `Glyphs.cs` and `Ledger.cs`: unique characters, so survival and resurrection are counted and order is never read.
- `Workload.cs`: the Q7 (a) sizes, recorded as an assumption.
- `Utf16.cs`, `Port.cs`, `RedPhase.cs`.
- `Conformance/MergeCases.*.cs`: the case bodies, shared by the conformance facts and the falsification.
- `Properties/InterleavingHarness.cs` and `SessionStep.cs`: seeded generator, greedy shrinker and trace printer.
- `Falsification/WholeFieldLastWriterWins.cs`: Lamport-stamped whole-text LWW, otherwise a correct port.

Also changed: `tests/Yello.Tests.Merge/Yello.Tests.Merge.csproj`. The `--ignore-exit-code 8` switch is removed, because
the suite now has cases, and the stale comments are rewritten.

### Requirement traits

Every conformance class carries `Suite=Merge`, `AR-18`, `AD-12` and one of `FR-31`, `FR-33`, `NFR-4`. The M-4 budgets
also carry `NFR-3`; admission also `AD-11` and `AR-20`; round trip also `AD-14`. The gates carry `AR-18` and `AD-12`.

## Step 5 - Validation

- Prerequisites: the story is approved with clear ACs, and the xunit.v3 framework is present. Pass.
- Every red scaffold is `Skip = "red: AC<n> - ..."` and asserts the expected behaviour. There are no placeholder
  assertions. Pass.
- The two green classes are deliberate standing gates. The falsification proves the suite discriminates with no
  candidate present, which is the AD-12 record AC3 asks for. StrictSuites holds AR-35. Pass.
- No new package and no new project. FsCheck and CsCheck were not used; the property harness is hand-written. Pass.
- No product code: nothing in a production project changed. Pass.
- Build: `dotnet build Yello.slnx`, 0 warnings, 0 errors.
- No browser session or CLI was left running, and no temporary artifact was written outside `_bmad-output/test-artifacts/`.

### Activation order (riskiest assumption first)

0. Move `PortStandIn/` into `Yello.Merge`, with no candidate yet. Confirm the falsification class is still 9/9 green:
   that proves the moved port is the one the suite was written against.
1. `AdmissionConformanceTests.A_replica_that_withdrew_a_change_keeps_editing_and_still_converges`, then its sibling
   withdraw case. This is the riskiest port assumption: that withdrawing a refused local change (AR-20) composes with
   the "has" exchange and causal delivery. A per-origin contiguous version vector breaks here, and finding that late
   would reopen the interface (Q4).
2. `RegionConformanceTests`: the lone editor first, then M-1, then M-2.
3. `ExactlyOnceConformanceTests`, `TextIndexConformanceTests`, then the rest of `AdmissionConformanceTests`.
4. `RoundTripConformanceTests`.
5. `ScaleConformanceTests`: M-5, then M-6.
6. `InterleavingPropertyTests`: the PR budget, then the seeds. Run the Nightly case once by hand with `--explicit on`.
7. `MergeBudgetTests`, alone (they are in `MergeTimingCollection`).
8. The registrations in Host and Client `Program.cs`, then `MergePortGateTests` with each plant, then
   `MergeAssemblyBootTests`.

Activate one at a time, confirm it fails for the stated reason, then implement. A scaffold that is wrong is corrected and
the correction recorded in the Debug Log, not deleted.

### Gates the implementation is expected to touch

- **CS0436**: a type declared in both `PortStandIn/` and `Yello.Merge` fails the build. Delete the folder in the same
  change.
- **Ring rule** (`AllowedReferenceEdges`, `RingDependencyTests.Merge_types_depend_on_nothing_but_Contracts`): Merge
  keeps its one edge, to Contracts. Host -> Merge and Client -> Merge already exist. No edge changes in 7.1 (Q11 defers
  the Infrastructure edge to 7.2).
- **Client singleton ban** (`ClientStaticStateGateTests`): the Client registers the strategy with `AddScoped`. Keep
  `Yello.Merge` free of mutable statics.
- **AssemblyMarker**: its `<remarks>` say no implementation exists and AR-40a is open. Both become false.
- **Package pins** (`PackageVersionPinTests`): no package may be added.
- **Tier gates** (`DeploymentShapeTests`): `InterleavingNightlyTests` is `Tier=Nightly` and `Explicit = true`. Adding
  the Merge step to `nightly.yml` (Q10) must keep the workflow-shape assertions green.
- **Zero-test gate** (`TestingConventionTests`): already satisfied, because the switch is gone.

## Completion summary

- Checklist: `_bmad-output/test-artifacts/atdd-checklist-7-1-the-merge-port-and-its-conformance-suite.md`
- Story: `_bmad-output/implementation-artifacts/7-1-the-merge-port-and-its-conformance-suite.md`
- Key assumptions: the port surface above (Q1 (a), Q4 (a)), the Q6 (a) budgets, the Q7 (a) sizes, the Q8 (a) typed
  refusal, the Q9 (a) UTF-16 indexes and the Q10 (a) 200 and 5,000 sessions.
- Next workflow: `bmad-dev-story`.
