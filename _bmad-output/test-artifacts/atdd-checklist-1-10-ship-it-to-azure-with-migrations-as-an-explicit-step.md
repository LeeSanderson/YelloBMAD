---
stepsCompleted:
  [
    'step-01-preflight-and-context',
    'step-02-generation-mode',
    'step-03-test-strategy',
    'step-04-generate-tests',
    'step-04c-aggregate',
    'step-05-validate-and-complete',
  ]
lastStep: 'step-05-validate-and-complete'
generationMode: 'ai-generation'
executionMode: 'sequential'
lastSaved: '2026-10-25'
workflowType: 'testarch-atdd'
storyId: '1.10'
storyKey: '1-10-ship-it-to-azure-with-migrations-as-an-explicit-step'
storyFile: '_bmad-output/implementation-artifacts/1-10-ship-it-to-azure-with-migrations-as-an-explicit-step.md'
atddChecklistPath: '_bmad-output/test-artifacts/atdd-checklist-1-10-ship-it-to-azure-with-migrations-as-an-explicit-step.md'
detectedStack: 'backend'
generatedTestFiles:
  - 'tests/Yello.Tests.Architecture/MigrationAtStartupGateTests.cs'
  - 'tests/Yello.Tests.Architecture/ProbeGateTests.cs'
  - 'tests/Yello.Tests.Architecture/SecretHygieneTests.cs'
  - 'tests/Yello.Tests.Architecture/DeploymentShapeTests.cs'
  - 'tests/Yello.Tests.Architecture/OperationsRecordTests.cs'
  - 'tests/Yello.Tests.Slices/Deployment/DeploymentHosts.cs'
  - 'tests/Yello.Tests.Slices/Deployment/MigrationBundleEquivalenceTests.cs'
  - 'tests/Yello.Tests.Slices/Deployment/DatabaseIdentityTests.cs'
  - 'tests/Yello.Tests.Slices/Deployment/ProbeTests.cs'
  - 'tests/Yello.Tests.Slices/Deployment/ForwardedHeadersTests.cs'
  - 'tests/Yello.Tests.Slices/Deployment/RateLimitPartitionTests.cs'
  - 'tests/Yello.Tests.Slices/Deployment/DataProtectionPersistenceTests.cs'
  - 'tests/Yello.Tests.Slices/Deployment/DatabaseUnavailableTests.cs'
  - 'tests/Yello.Tests.Slices/Deployment/SqlServerEngineFailFastTests.cs'
  - 'tests/Yello.Tests.Slices/Deployment/RunningHostCursorTests.cs'
  - 'tests/Yello.Tests.Slices/ContainerRuntimeProbeTests.cs'
  - 'tests/Yello.Tests.Client/StaticHostFallbackTests.cs'
inputDocuments:
  - '_bmad-output/implementation-artifacts/1-10-ship-it-to-azure-with-migrations-as-an-explicit-step.md'
  - '_bmad-output/test-artifacts/test-design-architecture.md'
  - '_bmad-output/test-artifacts/test-design-qa.md'
  - '_bmad-output/test-artifacts/atdd-checklist-1-9-prove-isolation-on-both-surfaces.md'
  - 'tests/TESTING-CONVENTIONS.md'
  - '_bmad/tea/config.yaml'
  - 'resources/knowledge/test-quality.md'
  - 'resources/knowledge/test-levels-framework.md'
  - 'resources/knowledge/test-priorities-matrix.md'
  - 'resources/knowledge/ci-burn-in.md'
  - 'resources/knowledge/data-factories.md'
---

# ATDD Checklist - Epic 1, Story 1.10: Ship it to Azure with migrations as an explicit step

## Step 1 - Preflight

**Stack:** `test_stack_type: auto` resolves to **`backend`** (`Yello.slnx`, `.csproj` files, no `package.json`, no Playwright/Cypress config). The browser suite is Playwright for .NET in `Yello.Tests.Client`, bound by `TESTING-CONVENTIONS.md`.

**Prerequisites - met.** The story is `ready-for-dev`, AC1-AC9 verbatim from `epics.md:925-967` (`status: final`), AC10-AC17 each cite their source. MTP with six test projects; .NET SDK 10.0.303; Rancher container runtime. No progress file existed for this story, so Create mode ran from the start.

**TEA flags:** `tea_use_playwright_utils: false`, `tea_use_pactjs_utils: false`, `tea_pact_mcp: none`, `tea_browser_automation: auto`, `tea_execution_mode: auto`. No `project-context.md` exists (the persistent-facts glob matched nothing).

**Knowledge loaded:** `test-quality`, `test-levels-framework`, `test-priorities-matrix`, `ci-burn-in` (this story builds CI), `data-factories`. Not loaded: `component-tdd` (no component level), `test-healing-patterns` (nothing to heal), Playwright Utils and Pact fragments (disabled, TypeScript-shaped). Where a fragment and `tests/TESTING-CONVENTIONS.md` disagree, the repository document wins.

**Planning inputs the orchestrator named:** PRD + addendum, the architecture spine, DESIGN/EXPERIENCE, epics, the readiness report, the 2026-09-13 sprint-change proposal and the SPEC were consulted through the story's own line citations (the story is a dense digest of them); the test design's R5, R11, PR-stage and B4 sections were read directly.

## Step 2 - Generation mode

**AI generation**, unconditional for a `backend` stack. No recording. Sources: the story's ACs, Tasks, Findings F1-F7 and open questions; the live schema (seven migrations); `Yello.Host/Program.cs`; the shared harness (`HostProcess`, `RunningHost`, `WireSession`, `WireDatabase`, `ProductActor`, `SqlServerEngine`); the existing gates (`RefusalBoundaryGateTests` A-9, `SpaceSwitcherGateTests` IL helpers, `ReleaseGatingSuiteTests`, `RepositoryLayout`).

## Step 3 - Test strategy

### What can run here, and what cannot

The orchestrator's rule and the story's shape agree: **everything that needs an Azure subscription is out of scope for automation.** The scaffolds cover what is decidable in this repository: text gates over the IaC, the workflows, the static-host config and the operational record; IL gates over production; the migration bundle against the shared engine; the Host's production-only behaviour (probes, forwarded headers, Data Protection persistence, the 503 mapping, the purge trigger, the clock step) over the wire on a local Host; the test-harness debts; and the identity/permission split on the local SQL engine. The Azure-only checks are listed at the end as manual verifications tied to Lee's prerequisites.

### Levels

- **ARCH** (`Yello.Tests.Architecture`) - text gates over `infra/`, `.github/`, `docs/operations.md`, `Yello.Client/wwwroot/staticwebapp.config.json` and `Yello.AppHost/Program.cs` (Q18 (a): text with anchors, no YAML or Bicep parser package); IL gates over production (no `Migrate`, no `AddHealthChecks`, probes free of the database). No container.
- **INT (wire)** (`Yello.Tests.Slices/Deployment/`) - a real `Yello.Host` process via `HostProcess` / `RunningHost`, on a database of the class's own on the **shared engine** (`MigratedDatabaseFixture`, or `SqlServerEngine.CreateDatabaseAsync` for a second database). No new engine. The probe case needs no engine at all (an unreachable server).
- **INT (db)** - bundle equivalence and the identity split, on the shared engine.
- **UNIT** (Slices, `InternalsVisibleTo` Host) - `CredentialHashingRateLimit.PartitionOf`, the sign-in issue instant, the `SqlServerEngine` probe seam, the runtime-requirement switch.
- **E2E** (`Yello.Tests.Client`) - one case: a deep link on the *published* output, served by a static host that applies `navigationFallback`.

### Scenario map

See "Step 4 - scaffolds generated" below: every row there is one scenario, with its id, AC, level, priority and status. The map was settled here and is not repeated.

### Priorities

P0 for anything that would leave the deployed product unusable or silently non-compliant: migrations off the Host, secret hygiene, the replica cap, the free-limit flags, the order gate, CI strictness, forwarded proto (F3), key persistence (F1), the honest `503`, and the §6.4/AR-40c records. P1 for supporting shape (ingress details, action pinning, IPv6 /64 and mapped addresses, the tripwire query, prerequisite states, the cursor debt).

### Duplicate-coverage guard - deliberately not written

- **A-9** (`RefusalBoundaryGateTests.No_production_code_runs_on_a_timer_or_as_a_hosted_service`) already covers "no component queries the database on a fixed interval" (AC6's second clause). ARCH-002 adds only the `AddHealthChecks` call, which A-9 cannot see: the hosted service is registered inside the framework, not referenced by Yello's IL.
- **The pooled-connection case** (`Isolation/PooledConnectionCaseTests`, `Slices/PooledConnectionIsolationTests`) is AC7's standing guard. No new case; running it against the deployed database is P5.
- **The uniform `429`** stays proved by `SessionEndpointTests.Sign_in_past_the_limit_is_refused_with_429_alike_for_a_known_and_an_unknown_address`.
- **AC2's content** is already proved by `SpaceIsolationSchemaTests` against `MigrateAsync`; SLC-001 proves only that the bundle produces the same catalogue.

### Red-phase requirement

Every scaffold whose AC is not implemented is committed skipped with `red: AC<n> - <why> (Task n)`. Scaffolds that pass today stay active as standing gates.

## Step 4 - Red-phase scaffolds generated

**Execution mode: sequential.** `tea_execution_mode: auto` would resolve to subagents; generation stayed in-thread for the reason stories 1.5-1.9 recorded: C# under this analyser set has to be built and fixed here anyway. First builds failed on `MA0190` (x31, `GeneratedRegex` as partial methods), `S4017`, `S6605` (x2), `MA0181` (x8), `S8969` (x3), `xUnit1051` (x3), `S4457`, `CA2100`, `S3459`, `S1144`; all fixed. Worker A's output is the Architecture and Slices files; Worker B's is the one browser case.

**44 new cases: 6 active and green today (standing gates), 38 skipped.** No new test project, no new package, no new engine.

### `tests/Yello.Tests.Architecture` - 21 cases, no container

| id | Test | AC | Status |
|---|---|---|---|
| ARCH-001 | `MigrationAtStartupGateTests.No_production_assembly_applies_migrations` | 15, 2 | **ACTIVE - green today, standing gate, planted** |
| ARCH-002 | `ProbeGateTests.No_production_code_registers_the_health_check_service` | 6 | **ACTIVE - green today, standing gate, planted** |
| ARCH-003 | `ProbeGateTests.The_probe_handlers_depend_on_nothing_that_reaches_the_database` | 6 | skipped - red: AC6 |
| ARCH-004 | `SecretHygieneTests.No_configuration_or_deployment_file_holds_a_credential` | 3 | **ACTIVE - green today, standing gate, planted** |
| ARCH-005 | `SecretHygieneTests.The_hosts_appsettings_carry_no_connection_strings` | 3 | **ACTIVE - green today, standing gate, planted** |
| ARCH-006 | `SecretHygieneTests.Every_connection_string_reaches_the_container_as_a_key_vault_secret_reference` | 3 | skipped - red: AC3 |
| ARCH-007 | `DeploymentShapeTests.There_is_exactly_one_deployment_environment` | 4 | skipped - red: AC4 |
| ARCH-008 | `DeploymentShapeTests.The_container_app_runs_at_most_one_replica_in_single_revision_mode` | 1 | skipped - red: AC1 |
| ARCH-009 | `DeploymentShapeTests.The_database_pauses_at_the_free_limit_rather_than_billing_overage` | 5 | skipped - red: AC5 |
| ARCH-010 | `DeploymentShapeTests.A_metric_alert_fires_at_ten_percent_of_the_free_allowance` | 5 | skipped - red: AC5 |
| ARCH-011 | `DeploymentShapeTests.The_ingress_is_https_only_on_8080_with_both_probes_and_forwarding_on` | 6, 11 | skipped - red: AC6/AC11 |
| ARCH-012 | `DeploymentShapeTests.Ci_runs_the_architecture_suite_first_then_everything_with_the_runtime_required` | 10 | skipped - red: AC10 |
| ARCH-013 | `DeploymentShapeTests.The_revision_is_promoted_only_after_the_migration_job_and_only_after_green_tests` | 1 | skipped - red: AC1 |
| ARCH-014 | `DeploymentShapeTests.Every_third_party_action_is_pinned_to_a_commit_sha` | 10 | skipped - red: AC10 |
| ARCH-015 | `DeploymentShapeTests.The_static_host_falls_back_to_index_html_for_client_routes` | 13 | skipped - red: AC13 |
| ARCH-016 | `DeploymentShapeTests.The_apphost_runs_the_migration_before_the_host` | 4, 15 | skipped - red: AC4/AC15 |
| ARCH-017 | `OperationsRecordTests.The_data_protection_gate_is_recorded_with_its_five_prerequisites` | 8 | skipped - red: AC8 |
| ARCH-018 | `OperationsRecordTests.The_spellcheck_and_ime_egress_travels_with_the_gate` | 9 | skipped - red: AC9 |
| ARCH-019 | `OperationsRecordTests.The_session_context_defect_finding_and_the_maxdop_rule_are_recorded` | 7 | skipped - red: AC7 |
| ARCH-020 | `OperationsRecordTests.The_tripwire_is_an_operator_query_and_never_an_endpoint` | 8 | skipped - red: AC8 |
| ARCH-021 | `OperationsRecordTests.Each_operator_prerequisite_carries_a_state` | 8 | skipped - red: AC8 |

### `tests/Yello.Tests.Slices` - 22 cases, all on the one shared engine

| id | Test | AC | Status |
|---|---|---|---|
| SLC-001 | `Deployment/MigrationBundleEquivalenceTests.The_bundle_applies_exactly_the_schema_the_migrations_do` | 2, 15 | skipped - red: AC2/AC15 |
| SLC-002 | `Deployment/MigrationBundleEquivalenceTests.A_second_bundle_run_applies_nothing_and_succeeds` | 15 | skipped - red: AC15 |
| SLC-003 | `Deployment/DatabaseIdentityTests.The_app_identity_reads_and_writes_but_cannot_alter_isolation` | 15 | **ACTIVE - green today, standing gate** (ran against the engine, not skipped) |
| SLC-004 | `Deployment/ProbeTests.Both_probes_answer_from_process_state_with_the_database_unreachable` | 6 | skipped - red: AC6 |
| SLC-005 | `Deployment/ForwardedHeadersTests.Behind_ingress_the_forwarded_scheme_lets_the_host_issue_an_antiforgery_token` | 11 | skipped - red: AC11 |
| SLC-006 | `Deployment/ForwardedHeadersTests.With_forwarding_off_a_forwarded_scheme_is_ignored` | 11 | skipped - red: AC11 |
| SLC-007 | `Deployment/ForwardedHeadersTests.A_rotated_left_hand_forwarded_address_does_not_escape_the_window` | 11 | skipped - red: AC11 |
| SLC-008 | `Deployment/ForwardedHeadersTests.An_ipv4_address_and_its_mapped_ipv6_form_share_one_window` | 11 | skipped - red: AC11 |
| SLC-009 | `Deployment/ForwardedHeadersTests.Ipv6_addresses_share_a_window_per_64_bit_prefix` | 11 | skipped - red: AC11 |
| UNIT-001 | `Deployment/RateLimitPartitionTests.An_ipv4_mapped_address_keys_as_its_ipv4_form` | 11 | skipped - red: AC11 |
| UNIT-002 | `Deployment/RateLimitPartitionTests.Ipv6_addresses_key_on_their_64_bit_prefix` | 11 | skipped - red: AC11 |
| UNIT-003 | `Deployment/RateLimitPartitionTests.An_ipv4_address_keys_as_itself_and_a_missing_one_shares_the_empty_partition` | 11 | **ACTIVE - green today, standing gate** (Q10's "keep the shared partition") |
| SLC-010 | `Deployment/DataProtectionPersistenceTests.A_session_issued_before_a_cold_start_is_honoured_after_it` | 12 | skipped - red: AC12 |
| SLC-011 | `Deployment/DataProtectionPersistenceTests.After_a_cold_start_the_old_antiforgery_pair_still_signs_out_and_sign_out_still_ends_the_session` | 12 | skipped - red: AC12 |
| SLC-012 | `Deployment/DatabaseUnavailableTests.A_session_against_an_unreachable_database_is_a_uniform_503` | 14 | skipped - red: AC14 |
| SLC-013 | `Deployment/DatabaseUnavailableTests.A_cold_start_that_cannot_load_the_key_ring_is_a_503_not_a_sign_out` | 14, 12 | skipped - red: AC14/AC12 |
| SLC-014 | `Deployment/DatabaseUnavailableTests.Only_a_request_that_already_reached_the_database_starts_the_refusal_purge` | 14, 6 | skipped - red: AC14 |
| UNIT-004 | `Deployment/DatabaseUnavailableTests.A_sign_in_after_a_clock_step_back_is_issued_at_the_sign_out_instant_and_admitted` | 14 | skipped - red: AC14 |
| UNIT-005 | `ContainerRuntimeProbeTests.When_ci_requires_the_runtime_an_unconfigured_one_fails_rather_than_skips` | 10 | skipped - red: AC10 |
| UNIT-006 | `ContainerRuntimeProbeTests.Without_the_requirement_an_unconfigured_runtime_still_skips` | 10 | skipped - red: AC10 |
| UNIT-007 | `Deployment/SqlServerEngineFailFastTests.A_dead_runtime_fails_fast_with_the_probes_reason_and_the_failure_is_cached` | 16 | skipped - red: AC16 |
| SLC-015 | `Deployment/RunningHostCursorTests.Another_clients_requests_do_not_satisfy_the_harnesss_wait` | 16 | skipped - red: AC16 |

`Deployment/DeploymentHosts.cs` is a helper, not a case: the production-only settings the IaC sets (`ForwardedHeaders__Enabled`, plain `http` behind ingress), an unreachable connection string (`tcp:127.0.0.1,1`), and an empty per-process user profile (`LOCALAPPDATA`/`APPDATA`/`USERPROFILE`/`HOME`).

### `tests/Yello.Tests.Client` - 1 case, no container, no collection

| id | Test | AC | Status |
|---|---|---|---|
| E2E-001 | `StaticHostFallbackTests.A_deep_link_on_the_published_client_boots_and_routes` | 13 | skipped - red: AC13 |

It takes no collection and no fixture, so it starts nothing while skipped. Its scaffold contract `PublishedClientHost.StartAsync` throws naming Task 7.

### Seams and names the scaffolds propose (reached by name, correct on activation)

| Proposed | Where | Why by name |
|---|---|---|
| `SessionRevocation.IssueInstant(DateTimeOffset now, DateTimeOffset? validAfter)` | UNIT-004 | Q12 (a); does not exist |
| `SqlServerEngine.UnavailableReasonFor(bool runtimeConfigured, bool runtimeRequired)` - throws `InvalidOperationException` naming `YELLO_REQUIRE_CONTAINER_RUNTIME` when required and unconfigured | UNIT-005/006 | Q19 (a); the env var cannot be flipped in a process that runs container classes in parallel |
| `SqlServerEngine(Func<CancellationToken, Task<string?>> probe, Func<SqlServerContainerFixture> containerFactory)` | UNIT-007 | Task 9 (a) seam |
| Problem type `urn:yello:problem:service-unavailable` | SLC-012/013 | Task 5's suggestion |
| Log marker `RefusalPurgeClaimed`, written synchronously on the request that claims the purge | SLC-014 | Task 5's "a purge counter or log line" |
| Table `dbo.DataProtectionKeys` | SLC-010 | the EF store's default (Q7 (a)) |
| The probes mapped by a type of their own, not `Program` | ARCH-003 | IL cannot isolate a lambda in the composition root |
| `YELLO_MIGRATION_BUNDLE` / `YELLO_REQUIRE_MIGRATION_BUNDLE` | SLC-001/002 | Task 3 |

### Fixtures and seed data - every constraint checked

**No new fixture type, no new engine, no new package, no new project.** Container-backed classes take `MigratedDatabaseFixture` (a database of the class's own on the shared engine) or the `SqlServerEngine` assembly fixture directly for a second database; `ContainerTopologyGateTests` stays green (still 4 engines in a full run). `ProbeTests` and `SqlServerEngineFailFastTests` touch no engine at all.

- **`AccessRefusal` aged row (SLC-014)** - `20261024121107_SpaceBoundaryRefusals.cs:18-23, 39-51`: no foreign key, no row-level security predicate; all seven columns non-nullable and supplied; `Capability` = `ReadProjectsAndTasks` (a `Capability` member, stored as a string by `AccessRefusalConfiguration.cs:47-50`, within `nvarchar(64)`); `Kind` = `CrossSpace` (`AccessRefusalKind`, within `nvarchar(32)`); `Outcome` = 404 (what `AccessRefusalRecorder.OutcomeOf` writes for `CrossSpace`); `OccurredAt` 100 days back against the 90-day `AccessRefusalPurge.Retention`.
- **Accounts (SLC-010 to SLC-014)** are registered through `POST /api/v1/accounts`, so `UX_Account_NormalizedEmailAddress` (random address per case), the Personal Space, its Owner Membership (`UX_Membership_SpaceId_Owner`, `FK_Membership_*`) and the statuses hold by construction.
- **The data-only identity (SLC-003)** is a SQL login with `db_datareader` + `db_datawriter` in the class's database, generated name and password, dropped in a `finally`; `SpaceIsolationPolicy` (`InitialSchema.cs:236`) and `MAXDOP = 1` (`ActiveSpaceResolution.cs:58-60`) are read back as the administrator.
- **The catalogue (SLC-001)** names `SpaceIsolationPolicy` and `UX_Membership_SpaceId_Owner` (`InitialSchema.cs:171-175`, `filter: [Role] = N'Owner'`) as anchors.

## Step 5 - Validation and red-phase evidence

### Build

```
dotnet build Yello.slnx
Build succeeded.  0 Warning(s)  0 Error(s)
```

### Test run

```
dotnet test Yello.slnx --no-build
Test run summary: Passed!   total: 548   failed: 0   succeeded: 509   skipped: 39
```

Baseline at `9de3ff3`: **504 total / 503 passed / 1 skipped**. Delta: **+44 cases, +6 active and passing, +38 skipped.** No previously passing test changed. The first combined run had the Slices engine's SQL Server crash during start (`handle-crash.sh`, a core dump; 136 Slices failures, every one "class fixture threw in InitializeAsync"). Slices alone then passed (243 / 222 / 21 skipped), and the combined run repeated green. This is the memory-bound machine (`full-test-run-starves-browser-suite`), not a scaffold: no scaffold adds an engine.

### Red scaffolds fail for the stated reason

Fifteen scaffolds were un-skipped together, built and run one by one; every file was then restored from its in-memory bytes and the SHA-256 matched (`RESTORED-IDENTICAL: True`):

| id | Failed with |
|---|---|
| UNIT-001 | expected `203.0.113.7`, actual `::ffff:203.0.113.7` |
| UNIT-002 | the two addresses in one /64 keyed differently |
| SLC-005 | anti-forgery token behind ingress: expected `OK`, actual `InternalServerError` (F3) |
| SLC-007 | `JsonException` from `FetchAntiforgeryTokenAsync` - the token cannot be issued over http yet (F3), which every behind-ingress case meets first |
| SLC-010 | the same `JsonException` (F3 first; F1 is reached only once Q10 lands - see the activation order) |
| SLC-012 | expected `ServiceUnavailable`, actual `InternalServerError` (the unhandled revocation read) |
| SLC-014 | purge-claim lines expected 1, actual 0 |
| SLC-004 | `/health/live`: expected `OK`, actual `NotFound` |
| UNIT-004 | "`SessionRevocation.IssueInstant(...)` does not exist" |
| UNIT-005 | "`SqlServerEngine.UnavailableReasonFor(...)` does not exist" |
| UNIT-007 | "SqlServerEngine has no (probe, container factory) constructor" |
| ARCH-003 | `Assert.Single()`: no type maps `/health/live` |
| ARCH-013 | ".github/workflows/ci.yml does not exist" |
| ARCH-015 | "staticwebapp.config.json does not exist" |
| ARCH-017 | "docs/operations.md does not exist" |

The other Architecture text gates share the same file-existence preconditions as ARCH-013/015/017 and fail on them.

### The standing gates were planted - 2026-10-25

All four production plants in one build, each caught by its own gate; originals restored from memory in a `finally`, the planted new file deleted, SHA-256 identical, and `git status` shows no product file modified.

| # | Plant | Gate | Caught |
|---|---|---|---|
| 1 | New `Yello.Infrastructure/Persistence/PlantedStartupMigration.cs` calling `RelationalDatabaseFacadeExtensions.Migrate(context.Database)` | ARCH-001 | **yes**: "Yello.Infrastructure.Persistence.PlantedStartupMigration calls ...RelationalDatabaseFacadeExtensions.Migrate" |
| 2 | `builder.Services.AddHealthChecks();` in `Yello.Host/Program.cs` | ARCH-002 | **yes**: "Program calls ...HealthCheckServiceCollectionExtensions.AddHealthChecks" |
| 3 | `"Planted": "Server=x;Password=hunter2hunter2"` in `Yello.Host/appsettings.json` | ARCH-004 | **yes**: the `Password=` shape, naming the file |
| 4 | A `ConnectionStrings` section in `Yello.Host/appsettings.Development.json` | ARCH-005 | **yes**: names the file |

SLC-003 and UNIT-003 are active but **not yet planted**: SLC-003's plant (grant the identity `db_owner`) and UNIT-003's (key on the submitted address) belong to Task 13's plant table.

### Red-phase compliance

- 38 of 44 new cases are skipped with `red: AC<n> - <why> (Task n)`.
- 6 are active and green, each a standing regression gate: ARCH-001, ARCH-002, ARCH-004, ARCH-005, SLC-003, UNIT-003.
- Every scaffold compiles under warnings-as-errors. Types and members story 1.10 has not written are reached by name through reflection, or behind a scaffold contract that throws naming its Task.
- Every absence assertion is anchored on a presence in the same test (`UseSqlServer`, `AddRateLimiter`, `Yello.Host/appsettings.json`, `ConnectionStrings__yello`, the database resource, the migrate job, `SpaceIsolationPolicy`, the reading identity, a failing database-bound request, a non-sharing address, a Host on another database).

---

## Implementation checklist - activation order for dev-story

**Activate one at a time, confirm it fails for the stated reason, then implement. A scaffold that is wrong is corrected and the correction recorded in the Debug Log, not deleted.**

1. **SLC-013, then SLC-010 and SLC-011 - the riskiest assumption first.** Q7 (a) assumes the Data Protection key ring can live in the application database *and* fail honestly when that database is paused: a cold start that cannot load the ring must answer `503`, never mint a fresh in-memory key that voids every outstanding Session. If the EF key store cannot be made to do that, Q7 must move to (b) or (c) before anything is built on it. These Hosts run behind ingress (plain http), so they first fail at the anti-forgery fetch for F3's reason: implement Q10's forwarded proto (activating **SLC-005** and **SLC-006** on the way), confirm the three then fail for F1's reason, and only then persist the ring.
2. **SLC-001 and SLC-002 - the migration bundle (Task 3, F5).** The second riskiest assumption: that the bundle applies the batch-first RLS DDL exactly as `MigrateAsync` does. Build it for the machine's own runtime to run it locally (`-r win-x64` here; `linux-x64` in CI and the image) and set `YELLO_MIGRATION_BUNDLE`. Then **ARCH-016** with the AppHost migration resource (Q17).
3. **SLC-012, SLC-014, UNIT-004** (Task 5). **SLC-004 and ARCH-003** after SLC-014: the probe's log slice stays clean only once the purge trigger has moved off anonymous requests.
4. **SLC-007 to SLC-009, UNIT-001, UNIT-002** (Task 4's partition). Keep **UNIT-003** green.
5. **UNIT-005, UNIT-006, UNIT-007, SLC-015** (Task 9's test-estate debts). Run SLC-015 several times when confirming red: its failure is a race today (see its remarks).
6. **ARCH-006 to ARCH-011** with the Bicep (Task 6); **ARCH-012 to ARCH-014** with the workflows (Tasks 9-10); **ARCH-015 and E2E-001** with `staticwebapp.config.json` and a static host (Task 7); **ARCH-017 to ARCH-021** with `docs/operations.md` (Task 12).
7. **Keep ARCH-001, ARCH-002, ARCH-004, ARCH-005, SLC-003 and UNIT-003 green throughout**, and A-9 beside them.

### Commands

```
dotnet build Yello.slnx
dotnet test Yello.slnx
dotnet test --project tests/Yello.Tests.Slices/Yello.Tests.Slices.csproj --no-build -- --filter-namespace Yello.Tests.Slices.Deployment
dotnet test --project tests/Yello.Tests.Slices/Yello.Tests.Slices.csproj --no-build -- --filter-class Yello.Tests.Slices.ContainerRuntimeProbeTests
dotnet test --project tests/Yello.Tests.Architecture/Yello.Tests.Architecture.csproj --no-build -- --filter-class Yello.Tests.Architecture.DeploymentShapeTests
dotnet test --project tests/Yello.Tests.Client/Yello.Tests.Client.csproj --no-build -- --filter-class Yello.Tests.Client.StaticHostFallbackTests
```

MTP filters of different kinds AND together (`--filter-namespace` with `--filter-class` ran zero tests); run them separately.

## Manual verifications - Azure only, tied to Lee's prerequisites

Not automatable here, and not to be claimed by any test. Each belongs in `docs/operations.md` §11 as "not observed yet" until the operator observes it.

| Check | AC | Prerequisite |
|---|---|---|
| Ingress appends `X-Forwarded-For`/`-Proto` as SLC-005/007 assume; a browser sign-in works on the deployed client | 11, 1 | P1, P3, P6 |
| Key Vault secret references resolve through the managed identities; the container starts with `ConnectionStrings__yello` populated | 3 | P1, P2, P6 |
| Managed-identity SQL authentication for the app and migrate identities (and SqlClient 7.x's Entra support, Q8) | 3, 15 | P2, P6 |
| The two database users exist with the runbook's grants (SLC-003 proves the roles locally, not the Entra principals) | 15 | P2 |
| The `yello-migrate` job runs, the workflow waits for `Succeeded`, and promotion follows | 1 | P3, P6 |
| The free-amount-remaining metric exists under the name ARCH-010 reads; the alert reaches the action group; auto-pause at exhaustion | 5 | P1, P6 |
| The £30 consumption budget alerts at 50/80/100% | §6.3 | P1 |
| The platform's probes report healthy against `/health/live` and `/health/ready` | 6 | P6 |
| A cold start keeps a Session (AC12 on the platform), and one cold-start observation for AR-40b | 12 | P6 |
| Sign-in p95 on the real hardware (`deferred-work.md:147`); no work-factor change on its strength | NFR-5 | P6 |
| AR-40c: the vendor confirmation, then the pooled-connection case against the deployed database | 7 | P5 |
| Safari and Firefox keep the Session cookie on the real hostnames (F2) | 13 | P4 |
| The first hosted CI run is green, under 15 minutes, and fails rather than skips without Docker | 10 | P3, P6 |
| The §6.4 assertion is confirmed or amended by the operator | 8 | P7 |

**Local but manual (Task 8, Rancher `docker`):** the image builds; `docker run` on `8080` answers `/health/live` and issues a `Secure` cookie through `X-Forwarded-Proto: https`; probes answer `200` with no database; `docker image inspect` and a filesystem scan find no connection string or `Password=`. Record commands and output in the Debug Log.

## Notes and risks

- **Behind-ingress cases depend on Q10.** Every Host in `DeploymentHosts.BehindIngress`/`InAFreshContainer` runs on plain http, so SLC-007 to SLC-011 and SLC-013 fail at the anti-forgery fetch until forwarded proto lands. That is why Q10 is implemented inside step 1 above.
- **The fresh-profile directories** (`yello-profile-*` under the temp folder) are not deleted by the scaffolds. Delete them in a `finally` on activation.
- **SLC-012 reaches a different path once F1 lands** (the key-ring load instead of the revocation read). Its remarks say what to add if the revocation-read mapping is then unreachable over the wire.
- **SLC-014's "row still exists" check after the anonymous requests is weak** (the purge is fire-and-forget); the claim log line is the gate.
- **The text gates assume Bicep's `key: value` layout with single-quoted strings, and one workflow file holding the deploy job** (ARCH-013). If Q10's alternative `workflow_run` shape is chosen, correct ARCH-013 and record it.
- **ARCH-010's metric name and threshold unit** are to be confirmed against the free-offer documentation when the Bicep is written.
- **New `Requirement` trait values**: `PRD-6.4` and `AR-40c`. Nothing validates the `Requirement` format today; keep or rename on activation.
- **Container cost:** none new. Every container-backed class takes a database on its suite's one engine.

## Completion summary

- **Test files:** 16 files (15 new, 1 extended), 44 cases; checklist at `_bmad-output/test-artifacts/atdd-checklist-1-10-ship-it-to-azure-with-migrations-as-an-explicit-step.md`.
- **Story handoff:** `_bmad-output/implementation-artifacts/1-10-ship-it-to-azure-with-migrations-as-an-explicit-step.md` (story key `1-10-ship-it-to-azure-with-migrations-as-an-explicit-step`).
- **Next workflow:** `bmad-dev-story`.

---

**Generated by BMad TEA Agent** - 2026-10-25
