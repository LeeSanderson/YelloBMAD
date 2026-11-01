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
lastSaved: '2026-11-01'
workflowType: 'testarch-atdd'
storyId: '4.1'
storyKey: '4-1-invite-an-email-address-to-a-space-at-a-role'
storyFile: '_bmad-output/implementation-artifacts/4-1-invite-an-email-address-to-a-space-at-a-role.md'
atddChecklistPath: '_bmad-output/test-artifacts/atdd-checklist-4-1-invite-an-email-address-to-a-space-at-a-role.md'
detectedStack: 'backend'
generationMode: 'ai-generation'
executionMode: 'subagent (API/wire/gates in the main run; browser scaffolds by one subagent)'
generatedTestFiles:
  - 'tests/Yello.Tests.Slices/Spaces/ResolveActiveSpace/ActiveSpaceEndpointTests.IssueInvitation.cs'
  - 'tests/Yello.Tests.Slices/Spaces/ResolveActiveSpace/ActiveSpaceEndpointTests.IssueInvitationOutbox.cs'
  - 'tests/Yello.Tests.Slices/Spaces/ResolveActiveSpace/ActiveSpaceEndpointTests.IssueInvitationRace.cs'
  - 'tests/Yello.Tests.Slices/Spaces/ResolveActiveSpace/ActiveSpaceEndpointTests.IssueInvitationSchema.cs'
  - 'tests/Yello.Tests.Isolation/IsolationCases.Invitations.cs'
  - 'tests/Yello.Tests.Isolation/IsolationCases.cs'
  - 'tests/Yello.Tests.Isolation/Expectation.cs'
  - 'tests/Yello.Tests.Isolation/IsolationTimingTests.cs'
  - 'tests/Yello.Tests.Architecture/RefusalBoundaryGateTests.Invitations.cs'
  - 'tests/Yello.Tests.Architecture/SecretHygieneTests.Email.cs'
  - 'tests/Yello.Tests.Client/InvitationTests.cs'
  - 'tests/Yello.Tests.Client/TextSpacingTests.cs'
  - 'tests/Yello.Tests.Client/FakeYelloApi.Invitations.cs'
  - 'tests/Yello.Tests.Client/FakeYelloApi.cs'
  - 'tests/Yello.Tests.Shared/InvitationScaffold.cs'
  - 'tests/Yello.Tests.Shared/MailDrop.cs'
  - 'tests/Yello.Tests.Shared/WireDatabase.cs'
inputDocuments:
  - '_bmad-output/implementation-artifacts/4-1-invite-an-email-address-to-a-space-at-a-role.md'
  - '_bmad-output/planning-artifacts/prds/prd-YelloBMAD-2026-08-15/prd.md'
  - '_bmad-output/planning-artifacts/architecture/architecture-YelloBMAD-2026-08-17/ARCHITECTURE-SPINE.md'
  - '_bmad-output/planning-artifacts/ux-designs/ux-YelloBMAD-2026-08-18/DESIGN.md'
  - '_bmad-output/planning-artifacts/ux-designs/ux-YelloBMAD-2026-08-18/EXPERIENCE.md'
  - '_bmad-output/planning-artifacts/epics.md'
  - '_bmad-output/planning-artifacts/implementation-readiness-report-2026-08-22.md'
  - '_bmad-output/planning-artifacts/sprint-change-proposal-2026-09-13.md'
  - '_bmad-output/specs/spec-yello/SPEC.md'
  - '_bmad-output/test-artifacts/test-design-architecture.md'
  - '_bmad-output/test-artifacts/test-design-qa.md'
  - '_bmad-output/test-artifacts/atdd-checklist-3-1-create-and-rename-a-space.md'
  - 'tests/TESTING-CONVENTIONS.md'
  - '_bmad/tea/config.yaml'
  - 'resources/knowledge/data-factories.md'
  - 'resources/knowledge/test-quality.md'
  - 'resources/knowledge/test-levels-framework.md'
  - 'resources/knowledge/test-priorities-matrix.md'
---

# ATDD Checklist - Epic 4, Story 4.1: Invite an email address to a Space at a Role

**Date:** 2026-11-01
**Author:** Lee (run unattended by the story-cycle orchestrator)
**Primary test level:** INT over the wire against a real Host process and SQL Server (`Yello.Tests.Slices`, partials of
the existing `ActiveSpaceEndpointTests`), isolation rows on both surfaces (`Yello.Tests.Isolation`), ARCH gates
(`Yello.Tests.Architecture`), and browser cases against the network-edge fake (`Yello.Tests.Client`).

---

## Story summary

An Owner or Admin posts `{emailAddress, role}` to a new Space-scoped route. The slice takes a Space-named lock, refuses
an existing Member or a live pending Invitation (both disclosable), inserts a `Pending` Invitation with a hashed token
and a database-clock `ExpiresAt`, renders one email into a policy-free outbox row in the same transaction, and answers
`204`. The dispatcher sends after commit; a recovery sweep runs at process start and on inbound authenticated traffic,
never on a timer. The response never depends on whether the address has an Account.

## Step 1 - Preflight

- **Stack:** `test_stack_type: auto` resolves to **`backend`** (`Yello.slnx`, `.csproj`, no `package.json`), as every
  earlier run recorded. The browser level is Playwright for .NET in `Yello.Tests.Client`, bound by
  `TESTING-CONVENTIONS.md`.
- **Prerequisites met.** The story is `ready-for-dev`. AC1-AC8 are verbatim from `epics.md` (`status: final`), and
  AC9-AC13 cite their sources. No progress file existed for this story, so Create mode ran from the start. Docker
  (Rancher) answered `29.5.3`.
- **TEA flags:** `tea_use_playwright_utils: false`, `tea_use_pactjs_utils: false`, `tea_pact_mcp: none`,
  `tea_browser_automation: auto`, `tea_execution_mode: auto`. No `project-context.md` exists.
- **Knowledge loaded:** `data-factories`, `test-quality`, `test-levels-framework`, `test-priorities-matrix`. Where a
  fragment and `TESTING-CONVENTIONS.md` disagree, the repository document wins.

## Step 2 - Generation mode

**AI generation**, unconditional for a `backend` stack. No recording. The sources were:

- the story (ACs, Tasks, Locking, Testing requirements, open questions);
- the live schema;
- the harness (`HostProcess`, `WireSession`, `SpaceSeeding`, `TaskWire`, `WireDatabase`, `ProductActor`,
  `IsolationWorld`, `DurationIndistinguishability`, `FakeYelloApi`);
- the existing gates (`RefusalBoundaryGateTests*`, `ApiTokenGateTests`, `SecretHygieneTests`, `IsolationCaseListTests`).

## Step 3 - Test strategy

### Decisions the scaffolds are written against

| Q | Source | Scaffolds that depend on it |
|---|---|---|
| Q1 cascade | Lee | `Deleting_a_Space_deletes_its_Invitations`, `An_Invitation_cascades_from_its_Space_restricts_its_issuer_and_references_no_Membership` |
| Q2 Admin may offer Admin | Lee | the issuer theory offers admin/member/viewer for both Owner and Admin; the client picker offers three for both |
| Q3 NFC + BIN2, no lookalike handling | Lee | `The_normalised_address_column_matches_the_Accounts_exactly`; normalised comparisons use `EmailAddressNormalisation.Normalise` |
| Q4 HttpClient REST, no package | Lee | no scaffold references an ACS SDK; `MailDrop` reads the ACS REST body shape |
| Q6 policy-free outbox, no Space column, delete on send | Lee | `The_outbox_has_no_Space_column_no_policy_and_no_foreign_key`, `Only_the_outbox_store_its_dispatcher_and_its_configuration_refer_to_the_outbox_row`, the sweep cases |
| Q5 (a) `204`, no body, no `Location` | assumed (recommended) | every success assertion; the I-7 byte comparison |
| Q7 (a) row deleted on send | assumed (recommended) | `One_issue_sends_exactly_one_email_and_the_outbox_row_is_deleted_once_sent` |
| Q8 (a) the form lives in an `InvitationSection` | assumed (recommended) | every client case |
| Q9 (a) copy | assumed (recommended) | the client copy assertions; the email assertions check content, not exact wording |
| Q10 (a) column default `DATEADD(day, 7, SYSDATETIMEOFFSET())` | assumed (recommended) | `ExpiresAt_defaults_to_seven_days_after_the_database_clock`; the race insert omits `ExpiresAt` |
| Q11 (a) stored `Pending/Accepted/Revoked` | assumed (recommended) | `An_expired_pending_Invitation_does_not_block_a_reissue_and_a_live_one_does` asserts the expired row stays `Pending` |
| Q12 (a) `409 invitation-pending` | assumed (recommended) | the pending, expired and race cases |

### New assumptions this run made (no question in the story covered them)

- **Q13 (a), the recording sender. This is the dominant finding.**
  - **The problem.** Every wire suite starts `Yello.Host` as a separate **Production** process, so no test can replace
    a service in it. The story's "tests replace the sender with a recording fake" is therefore not buildable as
    written.
  - **What the scaffolds assume:**
    - The sender is chosen by configuration: `ConnectionStrings:acs` gives the ACS adapter. Otherwise `Email:DropFolder`
      gives the file-drop sender, in any environment. Otherwise startup fails loudly.
    - The file drop writes one whole `*.json` file per message, in the ACS Email REST body shape.
    - `Email:Sender` is plain configuration.
  - **The consequence.** `HostProcess.CreateStartInfo` must gain a default `Email__DropFolder`. Without it, every
    Host-starting test in the solution fails at startup the moment AC11's "fails loudly" lands.
  - All of this is in one file, `tests/Yello.Tests.Shared/MailDrop.cs`.
- **The problem types.** An unknown, empty or missing Role answers the same `400 role-not-offerable` as `owner`. An
  implausible address answers a new `400 invalid-email-address`. Both live in `InvitationScaffold.cs`.
- **The piggyback throttle must not be consumed by the start-up kick.** Otherwise a Host started a moment ago will not
  sweep on its first authenticated request for a minute.
- **The forced rollback** is an `AFTER INSERT` trigger on `IdempotencyRecord` that throws for the case's key. This
  relies on the pipeline recording the key inside the same unit of work, after the endpoint has enqueued, which the
  story states.
- **The permanent duration oracle** is "an issue (`204`) against an already-a-member refusal (`409`)". That refusal
  stops after the lock and the Membership read, which is the planted handler's shape, reached through the product. A
  process Host cannot be given a planted handler. If the 20% method cannot tell these two apart, the I-7 duration proof
  is masked. Record it as masked; never delete it.
- **Gate (ii)'s `ToQueryString()` half** cannot compile before the predicate's type exists. The dev phase writes it as
  a Slices test when it adds the predicate. The structural half is
  `The_shared_actionable_predicate_reads_the_database_clock_and_never_a_CSharp_clock`.

### Container budget

- **No new engine, no new fixture type, and no new container-backed class.** The Slices scaffolds are four new partial
  files of `ActiveSpaceEndpointTests`, so they use its one database on the Slices engine.
- The isolation rows and the timing oracle run on the existing `IsolationWorldFixture`.
- The browser class joins `ClientSurfaceCollection`.
- No new package and no new project.

### Scenario map

| id | AC | Level | Pri | Scenario | File |
|---|---|---|:--:|---|---|
| 4.1-ISS-001 (x2) | 1, 3, 8, 12 | INT | P0 | Owner/Admin offer admin, member, viewer: `204`, no body, no `Location`. Each row has exactly the Space, the Role, `Pending`, the address verbatim and normalised, the issuer id and name, a distinct 32-byte hash, and `ExpiresAt` = DB now + 7d | `.IssueInvitation` |
| 4.1-ISS-002 (x2) | 1 | INT | P0 | Member/Viewer: `403 insufficient-role` naming `issue-and-revoke-invitations`, one `InsufficientRole` row, nothing written, no email | `.IssueInvitation` |
| 4.1-ISS-003 | 1 | INT | P0 | Non-member: the boundary `404`, byte-identical to an absent Space, with a `CrossSpace` row. Nothing written | `.IssueInvitation` |
| 4.1-ISS-004 (x4) | 2 | INT | P0 | `owner`, unknown, empty and null Role each give `400 role-not-offerable`. Anchor: `member` gives `204` | `.IssueInvitation` |
| 4.1-ISS-005 | 3 | INT | P1 | Plausible edges (tag/sub-domain, non-ASCII, exactly 254) each give `204` | `.IssueInvitation` |
| 4.1-ISS-006 | 3 | INT | P1 | 11 implausible inputs each give `400 invalid-email-address`, not echoed, nothing written. Anchor: `204` | `.IssueInvitation` |
| 4.1-ISS-007 | 4 | INT | P0 | A Member's address (re-cased and padded) and the issuer's own address each give `409 already-a-member`. Anchor: `204` | `.IssueInvitation` |
| 4.1-ISS-008 | 4, 5 | INT | P0 | A member of another Space is invited like anyone (`204`) | `.IssueInvitation` |
| 4.1-ISS-009 | 5 | INT | P0 | No Account vs an Account: identical status, body, content type and header names. One email each, the same text apart from the token | `.IssueInvitation` |
| 4.1-ISS-010 | 13 | INT | P0 | A second issue of the same address gives `409 invitation-pending`. One row and one email | `.IssueInvitation` |
| 4.1-ISS-011 | 8, 13 | INT | P0 | A seeded expired Invitation does not block (`204`, two rows, both still `Pending`). A seeded live one does (`409`) | `.IssueInvitation` |
| 4.1-ISS-012 | 6 | INT | P0 | An idempotent replay gives the same `204`, one row and one email. Anchor: a later issue is sent | `.IssueInvitation` |
| 4.1-ISS-013 | 12 | INT | P1 | Deleting the Space deletes its Invitation | `.IssueInvitation` |
| 4.1-ISS-014 | 5 | INT | P0 | The Host log holds neither address form, the token, the Space name nor the issuer name | `.IssueInvitation` |
| 4.1-OBX-001 | 6 | INT | P0 | One issue gives exactly one email from the configured sender, and the row is deleted | `.IssueInvitationOutbox` |
| 4.1-OBX-002 | 6 | INT | P0 | A failure after the enqueue (trigger) leaves no Invitation, no row and no email. Anchor: a later issue is sent | `.IssueInvitationOutbox` |
| 4.1-OBX-003 | 6 | INT | P0 | A row seeded before start is sent by the process-start sweep with no request, then deleted | `.IssueInvitationOutbox` |
| 4.1-OBX-004 | 6 | INT | P0 | A row seeded after start is sent on the next authenticated request | `.IssueInvitationOutbox` |
| 4.1-OBX-005 | 6 | INT | P1 | A claim older than 5 minutes is re-sent. A claim 1 minute old is left alone (`Attempts` = 1) | `.IssueInvitationOutbox` |
| 4.1-OBX-006 | 7 | INT | P0 | I-14: the email names the Space, "Member" and the issuer. None of 9+ withheld names or addresses appear (Projects, Task, Label, the other Space and its Project, Member, Admin, the issuer's address) | `.IssueInvitationOutbox` |
| 4.1-OBX-007 | 7 | INT | P0 | The link is `{ClientOrigin}/invitations/{token}`: not `yello_pat_`, no Space id or name, SHA-256 equal to `TokenHash`. The expiry is shown as a UTC date | `.IssueInvitationOutbox` |
| 4.1-RACE-001 | 13 | INT | P0 | Hold `Yello/Invitations/{spaceId}`, issue, wait on `LCK_M_%`, insert the competitor, commit. Result: `409`, one row | `.IssueInvitationRace` |
| 4.1-SCH-001..006 | 1, 3, 6, 8, 12, 13 | INT (catalogue) | P0 | The policy predicates (FILTER, BLOCK AFTER INSERT/UPDATE); the FKs (Space CASCADE, Account NO_ACTION, no Membership); the address columns equal the Account's (BIN2); the `ExpiresAt` default; the unique `TokenHash` and no `State`-filtered unique index; the outbox has no Space column, policy or FK | `.IssueInvitationSchema` |
| ISO rows (x6) | 1, 5, 9 | ISO | P0 | `FOREIGN-SPACE` (`BoundaryNotFound`, nothing written), `RECORD-KIND-...-MEMBER`/`-VIEWER` (`RoleRefused`), `TOKEN-OWN-OTHER-SPACE-ISSUE-INVITATION`, `NO-ACCOUNT-ORACLE` (bytes), `ISO-TIMING-...-NO-ACCOUNT-ORACLE` (duration). All are `PendingUntil: "4.1"` | `IsolationCases.Invitations.cs` |
| 4.1-TIME-ORACLE (x2) | 5 | ISO timing | P0 | The method must tell an issue from an already-a-member refusal, on both surfaces | `IsolationTimingTests.cs` |
| 4.1-ARCH-001..011 | 1, 2, 3, 6, 7, 8, 10 | ARCH | P0 | The contract has exactly `EmailAddress, Role`. `SpaceId`/`Role` are init-only. Only the store and configuration read `TokenHash`. The token is 256 bits with its own prefix and stored as SHA-256. Gate (i): one predicate compares `ExpiresAt`. Gate (ii): the DB clock, no C# clock. The template takes 5 string/Uri/date inputs. The outbox has named readers. The client invite predicate equals the matrix. The email rule and the client mirror agree. The problem types exist with client copy naming 4.1 | `RefusalBoundaryGateTests.Invitations.cs` |
| 4.1-ARCH-012 | 5 | ARCH | P0 | **Active, green today.** No `[LoggerMessage]` parameter is named for an address, recipient, token, link, subject, body or email | `RefusalBoundaryGateTests.Invitations.cs` |
| 4.1-INFRA-001 | 11 | ARCH | P1 | The ACS email service, managed domain and communication service exist. `yello-acs-connection` comes from `listKeys()`. `ConnectionStrings__acs` is a `secretRef`. `Email__Sender` is a plain value. The slot comment is gone | `SecretHygieneTests.Email.cs` |
| 4.1-CLI-001..014 (26 cases) | 1, 2, 3, 10 | Browser | P0 | See "Client scaffolds" below | `InvitationTests.cs`, `TextSpacingTests.cs` |

## Step 4 - Generated scaffolds (TDD red phase)

All of these are skipped with `Skip = "red: AC<n> - <why>"`, except one standing gate. Every file compiles with 0
warnings and 0 errors.

| File | Methods / cases | ACs | Today |
|---|---|---|---|
| `Slices/.../ActiveSpaceEndpointTests.IssueInvitation.cs` | 14 / 19 | 1, 2, 3, 4, 5, 8, 12, 13 | all skipped |
| `Slices/.../ActiveSpaceEndpointTests.IssueInvitationOutbox.cs` | 7 / 7 | 6, 7 | all skipped |
| `Slices/.../ActiveSpaceEndpointTests.IssueInvitationRace.cs` | 1 / 1 | 13 | skipped |
| `Slices/.../ActiveSpaceEndpointTests.IssueInvitationSchema.cs` | 6 / 6 | 1, 3, 6, 8, 12, 13 | all skipped |
| `Isolation/IsolationCases.Invitations.cs` | 6 rows (12 surface runs) | 1, 5, 9 | pending, so not in the cross product |
| `Isolation/IsolationTimingTests.cs` (one new theory) | 1 / 2 | 5 | skipped |
| `Architecture/RefusalBoundaryGateTests.Invitations.cs` | 12 / 12 | 1, 2, 3, 5, 6, 7, 8, 10 | 11 skipped; **1 active and green** |
| `Architecture/SecretHygieneTests.Email.cs` | 1 / 1 | 11 | skipped |
| `Client/InvitationTests.cs` | 14 / 24 | 1, 2, 3, 10 | all skipped |
| `Client/TextSpacingTests.cs` (one new theory) | 1 / 2 | 10 (ledger `:20`) | skipped |

**Totals:** 57 methods and 74 new cases: 73 skipped and 1 active. There are also 6 pending isolation rows.

**Green today: `No_log_message_takes_an_address_a_token_or_an_email_body`.** It is a standing regression gate and is
left active. Its anchor is that log messages exist. **Plant:** a temporary `[LoggerMessage] PlantedRecipient(ILogger,
string recipient)` in `Yello.Host/SpaceLog.cs` turned it red (1/1, naming `SpaceLog.PlantedRecipient(recipient)`). The
file was restored from a saved copy, and `git status` shows it unchanged.

### Supporting harness (no product code)

- `tests/Yello.Tests.Shared/InvitationScaffold.cs` is **temporary**. It holds the route, the problem types, the 7 days,
  the link path, the body builder and fresh addresses. Delete it when `InvitationRoutes` and `ProblemTypes` exist, as
  `LabelScaffold` was deleted.
- `tests/Yello.Tests.Shared/MailDrop.cs` is the recording sender for a process Host (Q13 (a)). It provides a per-case
  folder, its settings, and a reader of the ACS-shaped JSON. It waits on a file-system signal and a deadline, never a
  sleep.
- `tests/Yello.Tests.Shared/WireDatabase.cs` gains `AddInvitationAsync` (the R12 expired row), `InvitationsInAsync`,
  `OutboxRowsAsync`, `AddOutboxRowAsync` (unclaimed, or claimed n minutes ago by the DB clock), `DatabaseNowAsync`,
  and the `InvitationRow` and `OutboxRow` records.
- **The FK and constraint checks behind the seed helpers** are cited in the XML docs of `AddInvitationAsync` and of the
  race insert:
  - `FK_Invitation_Space_SpaceId`: the Space is seeded first;
  - the `IssuedByAccountId` FK to `Account`: a product-registered Account;
  - the unique `TokenHash`: 32 random bytes per row;
  - `BLOCK AFTER INSERT`: the connection carries the Space key;
  - `Id` is supplied, because no key is generated by the database.

  The Membership seeds reuse `SpaceSeeding` and `WireDatabase.AddMembershipAsync`. They satisfy
  `FK_Membership_Space_SpaceId`, `FK_Membership_Account_AccountId`, `AK_Membership_SpaceId_AccountId` and
  `UX_Membership_SpaceId_Owner`. The outbox seed needs nothing more, because the table has no FK and no policy
  (Lee's Q6).
- `tests/Yello.Tests.Client/FakeYelloApi.Invitations.cs` and one block in `FakeYelloApi.DecideAsync` answer `POST`
  to the invitations path only. Every other route answers as before.

### Client scaffolds (written by the browser worker)

The subagent's markup assumptions, which the dev phase must provide or correct here first:

- **The section.** A `.invitation-section` in `main`, with an "Invitations" heading.
- **The email field.** A `FormField` labelled "Email address", rendered as `input[type=email]` with
  `autocomplete="off"` and a random name per render. Errors are shown through `aria-invalid` and `aria-describedby`.
- **The Role picker.** A `<fieldset>` with `<legend>Role</legend>` and exactly three radios, "Admin", "Member" and
  "Viewer", none checked. The section mentions "Owner" nowhere.
- **The controls.** A "Send invitation" button and a `role="status"` line reading "Sending.". The form is `novalidate`.
- **On `204`.** Clear the address and the Role, refocus the email input, and announce "Invitation sent.".
- **Lost answer (an assumption the story does not fix).** "Not sent." is shown, the address and Role are kept, and the
  same key is reused on resend.
- **The tests:**
  1. Owner and Admin find the form.
  2. Member and Viewer have no affordance (anchored on an Admin).
  3. The picker offers exactly three Roles and never Owner.
  4. No Role is checked at first render.
  5. The email field is not autofillable.
  6. The posted body is exactly `emailAddress` and `role`, with a key.
  7. A resend reuses the key.
  8. Success clears the form, keeps focus and announces no `@`.
  9. Both 409s are shown beside the field.
  10. `role-not-offerable` is shown beside the group.
  11. Client-side refusals send nothing.
  12. The 403 shows the capability copy.
  13. The 404 shows the Space refusal surface.
  14. The form reflows at 320×256.

  `TextSpacingTests.Text_spacing_and_text_only_zoom_clip_nothing_in_the_invite_form` adds two more cases.

## Activation order (riskiest assumption first)

Activate one at a time, confirm it fails for the stated reason, then implement. A scaffold that is wrong is corrected
and the correction recorded in the Debug Log, not deleted.

0. **Harness first (Q13).** Choose the sender by configuration and add the default `Email__DropFolder` to
   `HostProcess.CreateStartInfo`. Then rerun one existing Slices class to prove nothing else broke.
1. **`IsolationTimingTests.The_duration_method_tells_an_issue_from_one_that_skips_the_outbox_and_token_work`**, then
   `ISO-TIMING-ISSUE-INVITATION-NO-ACCOUNT-ORACLE`. This is the riskiest assumption: that the 20% method can see the
   token and outbox work at all. If the oracle is not caught, I-7's duration proof is masked. Record it and raise it;
   do not widen the tolerance.
2. **`A_failure_after_the_enqueue_rolls_back_the_Invitation_and_the_outbox_row_and_sends_nothing`**. This proves the
   enqueue is in the same transaction and the dispatch comes after the commit (plants 3 and 4).
3. **`An_issue_racing_a_pending_Invitation_for_the_same_address_reads_it_only_under_the_Space_lock`** (plant 10).
4. **`An_expired_pending_Invitation_does_not_block_a_reissue_and_a_live_one_does`**, the `ExpiresAt` default, and AC8
   gates (i) and (ii) (plants 8 and 9).
5. The core issue and refusal cases, the schema cases, and the six isolation rows (clear every `PendingUntil` before
   review, GATE-003).
6. The outbox sweeps: process start, piggyback and stale claim (plant 6).
7. Email content: I-14 (plant 7) and the link/token case. Then the token, template and outbox-reader gates.
8. The client predicate, email-rule mirror and problem-type copy gates. Then the browser cases (Debug build of
   `Yello.Client` first; plants 12, 13 and 14).
9. The ACS Bicep gate (plant 15).

## Implementation guidance

- **Endpoint:** `POST /api/v1/spaces/{spaceId:guid}/invitations`, which takes `{emailAddress, role}` and answers
  `204`. On failure it answers `400 role-not-offerable`, `400 invalid-email-address`, `409 already-a-member`,
  `409 invitation-pending`, `403 insufficient-role` or the boundary `404`.
- **Tables:** `Invitation` (under the policy) and `OutboxMessage` (policy-free).
- **Lock:** `Yello/Invitations/{spaceId:D}`.
- **Configuration:** `Email:DropFolder`, `Email:Sender` and `ConnectionStrings:acs`.
- **Client:** `InvitationSection.razor` under `InvitationCapabilities.PermitsIssueInvitations`.
- **Active gates the dev phase amends** (each must stay green until then):
  - `SpaceScopedEntities` gains `Invitation`;
  - the Space settings union gate gains `IssueAndRevokeInvitations`;
  - `SpaceIsolationSchemaTests.TablesThatCannotMove` gains `Invitation`;
  - the AD-19 contract snapshot gains the new contract;
  - `Every_connection_string_reaches_the_container_as_a_key_vault_secret_reference` must pass with the new entry.

## Step 5 - Validation

- Prerequisites are satisfied. The story has clear ACs and the framework is present.
- Every scaffold asserts expected behaviour, with no placeholders. Each absence has an anchor in the same case.
- Every new case is skipped with a reason naming its AC, except the one standing gate, which was proved by a plant.
- The isolation rows are pending on 4.1, which is `ready-for-dev`, so GATE-003 is satisfied.
- No browser or CLI sessions were left open. The only temporary artifacts were per-case mail-drop folders, which tests
  create at run time, and a saved copy of `SpaceLog.cs` in the session scratchpad.
- **Build:** `dotnet build Yello.slnx` gives 0 warnings and 0 errors.
- **Full suite:** `dotnet test Yello.slnx --no-build --max-parallel-test-modules 1` gave 1,858 total, 1,792 passed,
  0 failed and 66 skipped, in 25m 49s. The baseline was 1,801 / 1,791 / 0 / 10.
  - The new entries are 57 methods: 56 skipped and 1 active.
  - The runner counts a skipped theory once, not per row, so the 74 cases above appear as 57 entries.
  - The 66 skips are the 10 that were already there plus 56 scaffolds.
- **Next workflow:** `bmad-dev-story`.
