# Story 4.1: Invite an email address to a Space at a Role

Status: ready-for-dev

Epic: 4 — Bring people into one piece of your work (the first story of the epic)
Story key: `4-1-invite-an-email-address-to-a-space-at-a-role`

**Requirements owned.** From the Story Coverage Index (`epics.md:369`): "4.1 | **FR-10, FR-39**, AR-28, AR-31, AR-33".

- **FR-10** (`prd.md:258-269`). The FR has these clauses:
  - "An Owner or Admin can invite an email address to a Space at a specified Role."
  - "Members and Viewers cannot issue Invitations."
  - "There is no domain restriction, no allowlist, and no requirement that the address already have an Account."
  - "exactly one Space and exactly one Role, fixed at issue time."
  - "cannot be issued at Owner Role."
  - "Issuing an Invitation to an address that already holds Membership in that Space is refused."
  - "The response to the issuer is identical whether or not the address corresponds to an existing Account."
  - "The Invitation record retains its terminal state once it leaves pending ... rather than being deleted."
- **FR-39** (`prd.md:643-652`). The email "names the Space and the Role offered, and identifies who issued it" and "discloses nothing about the Space's contents, its other Members, or any other Space". `[ASSUMPTION: the acceptance route expires after a fixed period — 7 days]` is PRD §12 item 9 (`prd.md:900`), which is unconfirmed (`implementation-readiness-report-2026-08-22.md:649`, `:1240-1247`).
- **AR-28 / AD-23** (`epics.md:216`; `ARCHITECTURE-SPINE.md:213-217`). Responses are "identical in status, body and shape ... also identical in *duration*: the work performed does not branch on existence". No endpoint returns an email address to anyone but an Owner or Admin of a shared Space.
- **AR-31 / AD-27** (`ARCHITECTURE-SPINE.md:245-249`). The rules are:
  - "exactly one predicate — `State = Pending AND ExpiresAt > now` — declared once as a shared specification";
  - it is evaluated "server-side inside the guarded statement's own `WHERE` clause, against the database clock";
  - "No job and no timer writes the lapsed state";
  - "Rows are never deleted on expiry".
- **AR-33 / AD-10** (`epics.md:221`; `ARCHITECTURE-SPINE.md:132-136`). Mail is "enqueued through an outbox in the same transaction as the triggering write and dispatched in-process on enqueue". The "recovery sweep ... runs at process start and otherwise piggybacks on inbound request traffic". "No component queries the database on a fixed interval."

**Also binding, though the coverage row omits them:**

- **Role capability matrix** (`prd.md:348`; `specs/spec-yello/role-capability-matrix.md:19`, `:42`). "Issue and revoke Invitations | ✔ | ✔ | — | —". "An Invitation can never be issued at Owner Role." It is already in code: `Capability.IssueAndRevokeInvitations` (`Yello.Domain/Memberships/Capability.cs`) and its matrix row in `RoleCapabilityMatrix.cs`.
- **AD-1 / AD-2 / AD-3 / AD-4 / AD-20.** Role comes from the Membership row per request. A new Space-scoped table carries a non-nullable `SpaceId`, an RLS policy and an EF filter. A non-member gets the boundary `404` (`CrossSpace` record), and a Member or Viewer gets `403` (`InsufficientRole` record). Both surfaces get the same behaviour.
- **AD-18** (`ARCHITECTURE-SPINE.md:182-187`). Issue accepts an `Idempotency-Key`, and a replay does not enqueue a second email.
- **AD-28** (`:251-255`). The token "identifies which offer is in play and is never the authority for accepting it". 4.1 mints the token and the link. 4.3 builds the route the link opens.
- **AD-5** (`:106`) and **FR-12** (`prd.md:284-291`). An Invitation survives its issuer's demotion, removal and Account deletion, so the issuer reference must not cascade from `Membership` or `Account`.
- **§6.1 privacy** (`prd.md:763-765`; `epics.md:178`, owned in part by 4.1). Email addresses are visible only to Owners and Admins of a shared Space.
- **UX-DR27** (`epics.md:271`). Capabilities a Role lacks are **absent, not disabled**.
- **UX-DR34 / UX-DR38** (`epics.md:278`; `EXPERIENCE.md:151-155`, `:383-390`). No email address appears outside Space settings' Membership management, and none in any announcement.
- **NFR-6** (`prd.md:726`). No secret appears in a log. Treat the Invitation token and the invited address as secrets in logs (I-17).
- **NFR-9** (`prd.md:755`; `EXPERIENCE.md:370`). The invitation flow meets WCAG 2.1 AA.

**Ledger entries owned (`deferred-work.md`). Close each one, or re-own it with a reason:**

| Line | Entry | Severity | Owner per ledger | Covered by |
|---|---|---|---|---|
| `:425` | The Communication Services resource, its Key Vault secret `yello-acs-connection`, and `ConnectionStrings__acs` on the container (`infra/main.bicep:205-209`) | low | 4.1 | AC11, Task 8 |
| `:569` | Full-width lookalike addresses register as separate Accounts (NFC, not NFKC). This is a product decision, to be made "before invitations address people by email" | low | 4.1 (**Lee/PM**) | Q3 **[LEE]** |
| `:689` | **C1:** Invitations on a Space deletion, destroyed or kept for SM-4? Decide the FK action before the table is created | medium | 4.1 (**Lee/PM**) | Q1 **[LEE]**, AC12 |
| `:20` | The 1.4.12 text-spacing / 200% text-only zoom cases. "Each later surface adds its own case" | medium | open, rolling | AC10 (the invite form) |
| `:532` | Space settings holds only Labels and the name. The Invitations section is attributed to 4.2 | low | 4.2, 4.4, 5.1, 6.3 | Q8. 4.1 creates the section with the form, and 4.2 adds the list |

**Depends on** (all done):

- 1.3: registration, `EmailAddressNormalisation`, the duration harness.
- 1.6: the pipeline, the matrix, refusal recording, idempotency.
- 1.8: the Token secret pattern.
- 1.9: the isolation case list and its gates.
- 1.10: Bicep and `SecretHygieneTests`.
- 2.4 and 3.1: the Space settings sections.
- 3.3: Space deletion by cascade.
- 3.4: the HOLDLOCK lesson and the `TokenOwnOtherSpace` gate.

**Explicitly not in this story:**

- **The pending Invitation list and revoke** (4.2).
- **The invitation view, acceptance and registration-while-accepting** (4.3). 4.1 produces the link `{ClientOrigin}/invitations/{token}`. Until 4.3 ships, that route renders the client's not-found. Record this; do not build a placeholder page.
- **The 100-Membership bound.** It lands on **acceptance**, not on issue (`epics.md:1753`; `implementation-readiness-report-2026-08-22.md:1372-1376`). Issue consults **no** bound.
- **The notification send record (FS-NFR-2 / SM-C4).** This is 4.6's (`epics.md:374`). See C5.
- **No commit, push, branch, Azure login or deployment.** The orchestrator commits. Bicep is written and validated with `az bicep build` only if the CLI is present locally. It is never deployed.
- **No new NuGet package and no new project without Lee** (CLAUDE.md). Q4 takes the route that needs neither.

## Story

As an Owner or Admin of a Space,
I want to invite someone by email address at a Role I choose,
So that I can bring a collaborator into one piece of my work without exposing anything else.

## Acceptance Criteria

AC1 to AC8 are reproduced **verbatim** from `epics.md:1627-1663` (`status: final`) and numbered by position. AC9 to AC13 come from the spines, the ledger and a survey of the code. Each one cites its source.

**AC1 — one Space, one Role, fixed at issue; absent for Members and Viewers** (FR-10, matrix)
**Given** an active Space
**When** an Owner or Admin issues an Invitation to an email address at a Role
**Then** the Invitation carries exactly one Space and exactly one Role, both fixed at issue time
**And** a Member or Viewer has no invite affordance present

> - **The route** is `POST /api/v1/spaces/{spaceId:guid}/invitations`, declared as `InvitationRoutes.Issue` in `Yello.Contracts/Invitations/InvitationRoutes.cs`, with a `InvitationsOf(Guid)` helper.
> - The route carries `.RequiresCapability(Capability.IssueAndRevokeInvitations)`.
> - It is served on **both** surfaces (Session and ApiToken), for FR-35 parity.
> - **The body** is `IssueInvitationRequest(string? EmailAddress, string? Role)`. It has exactly these two fields, and a contract-shape gate holds that, as `The_Space_request_contracts_carry_a_name_and_nothing_else` does.
> - **Refusals:**
>   - A Member or Viewer gets `403` `insufficient-role` with one `InsufficientRole` record.
>   - A non-member gets the boundary `404` with one `CrossSpace` record.
>   - Both come from the pipeline, never the slice (AR-3, AD-20).
> - **The schema** has no column or route by which Space or Role can change after insert:
>   - `Invitation.SpaceId` and `Invitation.Role` are `init`-only;
>   - there is a BLOCK AFTER UPDATE predicate;
>   - add a gate sibling of the `A_X_Space_can_only_be_set_when_it_is_created` gates.
> - **"No invite affordance present"** is UX-DR27: the control is absent from the DOM, never disabled or hidden by CSS. Members and Viewers cannot reach Space settings at all today (`SpaceCapabilities.PermitsSpaceSettings`). The new section must keep that true: add the new predicate to the union and to `The_clients_Space_settings_predicate_is_the_union_of_its_sections`.

**AC2 — Owner is not offerable** (FR-10, FR-42, matrix boundary)
**Given** the Role picker at issue
**When** it renders
**Then** Owner is not offerable, because ownership moves only by an Ownership Offer accepted under FR-42

> - **Client:** the picker has no Owner option.
> - **Server:** `Role = "owner"` is refused `400` with a new problem type `urn:yello:problem:role-not-offerable`, whatever the client offered. An unknown or missing Role is also `400`. Reuse the existing validation problem type if one fits, and record the choice.
> - **The picker has no default selection** (`DESIGN.md:473`; `EXPERIENCE.md:211`). Inviting at a Role is consequential.
> - **Which Roles an Admin may offer is Q2 [LEE].** The recommended option is Admin, Member and Viewer for both issuers, exactly as the matrix states. The alternative narrows an Admin to Member and Viewer.

**AC3 — any address** (FR-10)
**Given** any email address
**When** it is invited
**Then** it is accepted with no domain restriction, no allowlist, and no requirement that it already have an Account

> - The only check is plausibility, using the rule registration already uses: exactly one `@`, neither first nor last, no whitespace, control or Format characters, at most 254 characters.
> - **Extract** the rule from `RegisterAccountValidator.IsPlausibleEmailAddress` (private) into a Domain rule, for example `Yello.Domain/Accounts/EmailAddressRule.cs`. Registration and invitation then share one function, and there is no second copy.
> - Mirror the rule on the client as `SpaceNameRule` does, with a gate that the two agree (`RefusalBoundaryGateTests.Spaces.cs:242` is the model).
> - An implausible address is `400` with a stable problem type.
> - Store the address twice:
>   - verbatim, as `EmailAddress` (max 254), for display in 4.2;
>   - as `NormalizedEmailAddress`, through **`EmailAddressNormalisation.Normalise`**, in `nvarchar(254)` with the `Latin1_General_100_BIN2` collation. This matches the Account column exactly (story 2.6), so 4.3's comparison is one function and one collation.

**AC4 — already a Member is refused, and that refusal is disclosable** (FR-10)
**Given** an address that already holds a Membership in this Space
**When** it is invited
**Then** the request is refused
**And** this refusal *is* disclosable, because the issuer can already see that Membership

> - The answer is `409` with a new problem type, `urn:yello:problem:already-a-member`, and client copy beside the field.
> - The check is one Space-scoped read: does a `Membership` in the active Space have an `Account` whose `NormalizedEmailAddress` equals the normalised input? `Membership` is under RLS, and `Account` is policy-free and global.
> - The check runs **after** taking the Space-named application lock (see Locking), so a concurrent issue of the same address cannot slip past.
> - **This is the only branch that may depend on Accounts,** and it depends on Membership *in this Space*, never on whether an Account exists anywhere.

**AC5 — no Account-existence oracle** (FR-10, AR-28, AD-23, I-7, R2)
**Given** an address that does, and separately does not, already have a Yello Account
**When** each is invited
**Then** the response to the issuer is identical in status, body, shape and duration
**And** issuing an Invitation therefore never discloses whether someone uses Yello

> - **Status and body.** Success is `204 No Content` with no body and no `Location` (Q5 (a), the registration precedent). The two arms are then byte-identical with nothing to mask. `Results.Created` and `Results.Accepted` are banned by gate (`RefusalBoundaryGateTests.Tasks.cs:220`), and a `204` replays through the idempotency record.
> - **Duration.** The work does not branch on existence (AD-23):
>   - the handler never asks "does an Account exist for this address?";
>   - the email body, the outbox row and the token are built identically;
>   - the only Account-touching read is AC4's Membership-in-this-Space join, whose plan is the same index seek either way.
> - **The proof** is an I-7 arm for invitation. Use `tests/Yello.Tests.Shared/DurationIndistinguishability.cs` (21 interleaved samples per arm, median, 20% tolerance, server-side via `CompareMeasuredAsync`). Do **not** write a second harness (`TESTING-CONVENTIONS.md:458-490`).
>   - Arm A: a fresh address with no Account.
>   - Arm B: a fresh address registered as an Account with no Membership in the Space.
>   - Both arms are run on the Session surface. Add the ApiToken arm if the timing collection budget allows; it is the same pipeline (AD-4).
>   - It lives in a `DisableParallelization` timing collection.
>   - Keep the **permanent planted oracle** beside it, as `RegistrationDurationTests` does: a handler variant that skips the outbox and token work when an Account exists must fail the comparison.
> - **Logs.** `Program.cs:66-88` already filters `Microsoft.EntityFrameworkCore.Database.Command` to Warning, which keeps the INSERT statements out of the log. Keep it. No `[LoggerMessage]` takes the address or the token.

**AC6 — one email through the outbox, in the same transaction; no timer** (FR-39, AR-33, AD-10, A-9)
**Given** a successful issue
**When** the transaction commits
**Then** one email is enqueued through the outbox **in the same transaction** and dispatched in-process on enqueue
**And** the recovery sweep for messages unflushed by a crash runs at process start and otherwise piggybacks on inbound request traffic, so no timer ever wakes an idle database

> **This story builds the outbox that 5.1 (FR-43) reuses** (`ARCHITECTURE-SPINE.md:243`: "the AD-10 path already built for FR-39"). Build it as a general mechanism keyed by message kind, not as an Invitation-only path. The design is Q6 **[LEE]**. The recommended shape:
>
> - **Table `OutboxMessage`.** Columns:
>   - `Id`;
>   - `Kind` (stable code, for example `invitation-issued`);
>   - `Recipient`;
>   - `Subject`;
>   - `Body` (the rendered text, and HTML if wanted);
>   - `EnqueuedAt`;
>   - `ClaimedAt` (nullable);
>   - `Attempts`.
>
>   The row is **rendered at enqueue**, so the dispatcher never reads Space data. The dispatcher deletes the row on a successful send (Q7).
> - **Enqueue** goes through a port, `IOutbox.EnqueueAsync(OutboxEmail)` in Application, which the issue handler calls in the same unit of work as the Invitation insert. The store saves its own changes before the pipeline records the idempotency key, because `IdempotencyStore` refuses to save while other changes are pending (`Yello.Infrastructure/Persistence/IdempotencyStore.cs`).
> - **Dispatch on enqueue, after commit.** The pipeline commits **after** the endpoint returns (`ActiveSpaceResolution*.cs`, `ServeAsync` → `unitOfWork.CompleteAsync`). Signal the dispatcher only once the commit has happened and the status is a success. The 3.3 precedent is `response.OnStarting` with a status check (`ActiveSpaceEndpoints.DeleteAsync:159-173`). A cleaner alternative is a post-commit hook on `UnitOfWork`. Either way, a rolled-back issue must enqueue **and** send nothing: prove it with a forced rollback.
> - **The dispatcher** is a singleton in Infrastructure that runs fire-and-forget on its own scope and its own `YelloDbContext` (the `AccessRefusalPurge` precedent). It is **never** a `BackgroundService`, `IHostedService` or timer: `RefusalBoundaryGateTests.No_production_code_runs_on_a_timer_or_as_a_hosted_service` (`:701`) bans them, and A-9 is that gate. Its steps:
>   1. Claim rows with one guarded statement (`UPDATE TOP (n) ... SET ClaimedAt = SYSUTCDATETIME(), Attempts += 1 OUTPUT ... WHERE ClaimedAt IS NULL OR ClaimedAt < DATEADD(minute, -5, SYSUTCDATETIME())`, with `READPAST`). This uses the database clock, so the trigger and the sweep never double-claim.
>   2. Send each claimed row.
>   3. Delete each row that was sent.
>
>   Delivery is at least once: a crash between send and delete resends. Record that.
> - **The recovery sweep** has two triggers:
>   - **Process start:** fire-and-forget after `ApplicationStarted`, with logged failure. It does not block startup and it does not run migrations.
>   - **Piggyback:** a middleware modelled on `Yello.Host/Spaces/RefusalRetentionTrigger.cs` plus a throttle modelled on `RefusalPurgeSchedule` (an `Interlocked` claim, at most once per minute per process). It fires only on inbound authenticated requests.
>
>   Neither trigger is a timer.
> - **Proof.** Each of these is a separate test:
>   - the row exists in the same transaction (a forced rollback after enqueue leaves no `OutboxMessage` and no `Invitation`);
>   - one issue gives exactly one send through the recording fake sender;
>   - an idempotent replay gives no second row and no second send;
>   - a row left unclaimed (seeded, as if by a crash) is sent by the process-start sweep of a fresh Host, and separately by the piggyback trigger on the next authenticated request;
>   - the existing timer and hosted-service gate stays green, and a plant of a `PeriodicTimer` in the dispatcher turns it red.

**AC7 — the email names Space, Role and issuer, and nothing else** (FR-39, I-14, UJ-2)
**Given** the delivered email
**When** it is read
**Then** it names the Space, the Role offered and who issued it
**And** it discloses nothing about the Space's contents, its other Members, or any other Space

> - **Content** (Q9 holds the copy). Include only:
>   - the Space name;
>   - the Role's display name ("Admin", "Member" or "Viewer"; uppercase only through CSS, so the plain email says "Member");
>   - the issuer's **display name**, read from `Account.DisplayName` at issue. **Never** include the issuer's email address (UX-DR34; the email goes to someone not yet in the Space);
>   - the link;
>   - the expiry as a UTC date.
> - **Not in the email:**
>   - any Project, Task, Label or Status;
>   - any other Member's name or count;
>   - any other Space, including the issuer's Personal Space (UJ-2's climax, `EXPERIENCE.md:486`);
>   - the invitee's own Account status.
> - **The link** is `{ClientOrigin}/invitations/{token}`. `ClientOrigin` is the existing configuration key (`Yello.Host/Sessions/CrossOriginPolicy.cs:50`). The URL carries only the opaque token: no Space name or id (UX-DR18, `EXPERIENCE.md:177-178`).
> - **Proof (I-14).** In an isolation-world test, the issuer owns two populated Spaces with distinct, randomised Project, Task, Label and Status names and a second Member. Issue an Invitation from one Space, then assert:
>   - the recorded email contains the Space name, the Role and the issuer's display name;
>   - it contains **none** of the populated names, the other Space's name, the second Member's name or address, or the issuer's address.
>
>   The anchor is the same names injected into the template by a plant, which must turn the assertion red (`TESTING-CONVENTIONS.md:192`).

**AC8 — `ExpiresAt` and the one shared predicate** (AR-31, AD-27, A-10, R12)
**Given** the Invitation record
**When** it is stored
**Then** it carries an `ExpiresAt` evaluated only by the shared `State = Pending AND ExpiresAt > now` predicate, server-side inside each guarded statement's own `WHERE` clause against the database clock
**And** no job and no timer ever writes its expired state

> - **`ExpiresAt` is a `DateTimeOffset`** (A-15). It is set **from the database clock** at insert, as 7 days after `SYSDATETIMEOFFSET()`. Q10 (a) is a column default, or the value is computed in the insert statement. The point is that the stamp and the predicate share one clock. Seven days is PRD §12 item 9: every test that rests on it carries `[Trait("Assumption", "PRD-12-9")]`.
> - **`State`** is stored as its name (a string column, like `Role`). The values are `Pending`, `Accepted` and `Revoked` (Q11 (a); C2). **"Expired" is never stored.** It is `State = Pending AND ExpiresAt <= now`, derived on read, which is what AD-27 requires. 4.1 only ever inserts `Pending`.
> - **The predicate is declared once.**
>   - Declare it as an expression over the entity and a database-clock function, for example `OfferPredicates.Actionable` built on a `[DbFunction]` such as `DatabaseClock.Now()` mapped to `SYSDATETIMEOFFSET()`.
>   - Place it where Infrastructure and later stories (4.2, 4.3, 5.1-5.3) all reach it.
>   - It must translate to SQL, never evaluate client-side: no `TimeProvider` and no C# `DateTimeOffset.UtcNow` in it.
> - **Its first consumer is 4.1's own duplicate-pending check** (AC13). That gives AC8 a falsifiable test:
>   - a live pending Invitation blocks a re-issue;
>   - one seeded with `ExpiresAt` in the past (through `WireDatabase`, the R12 method) does not.
> - **Gates** (Architecture):
>   - (i) Only the shared declaration names `ExpiresAt` in a comparison. Any other `ExpiresAt >` or `<` in `Yello.Application` or `Yello.Infrastructure` outside it fails. Allow-list the existing `SpacePresence` and `IdempotencyRecord` expiries, which predate AD-27's Offer/Invitation scope, by name and with a reason, or show that their member names differ.
>   - (ii) The predicate's translated SQL contains `SYSDATETIMEOFFSET` (use `ToQueryString()` in a Slices test).
>   - (iii) **A-10** (`test-design-qa.md:308`; `test-design-architecture.md:319-327`) is owned by 5.3. 4.1 introduces `Invitation.State` first. Recommended: extend the existing timer/hosted-service ban with "nothing but the named transition stores writes `Invitation.State`". 4.1 writes it nowhere, because it only inserts. Otherwise ledger it to 5.3 explicitly.
>   - Each gate is proved by a plant.

**AC9 — isolation rows on both surfaces** (AD-4, I-1, I-2, I-7, I-13, I-14, A-4, A-5; `TESTING-CONVENTIONS.md:677-699`)

> The new route fails `Every_api_route_the_Host_maps_but_the_named_credential_routes_is_named_by_a_case` until it has rows. Add them to `tests/Yello.Tests.Isolation/IsolationCases.cs` under a heading "Story 4.1 - issue an Invitation". Each row states both surfaces:
>
> - `ISO-ISSUE-INVITATION-FOREIGN-SPACE` (I-2): `BoundaryNotFound` with a `CrossSpace` record, and a `Then` proving that no `Invitation` and no `OutboxMessage` row was written in the foreign Space.
> - `ISO-ISSUE-INVITATION-MEMBER` and `-VIEWER`: `RoleRefused("issue-and-revoke-invitations")`, nothing written.
> - `ISO-TOKEN-OWN-OTHER-SPACE-ISSUE-INVITATION` (`IsolationVector.TokenOwnOtherSpace`), required by the 3.4 gate `Every_Space_scoped_route_a_Token_reaches_has_a_row_with_a_Token_of_the_same_Account_bound_elsewhere`.
> - `ISO-ISSUE-INVITATION-NO-ACCOUNT-ORACLE` (I-7): byte-identical `204` bodies and headers for the two arms, on both surfaces. Duration is AC5's timing case. Name it in the row's remarks.
> - `ISO-INVITATION-EMAIL-DISCLOSES-NOTHING` (I-14), if the case list can host it. Otherwise it is a Slices/wire test named in AC7, with its API counterpart stated (3.4 Q4 precedent).
> - The rules for these rows:
>   - If ATDD lands rows with `PendingUntil: "4.1"`, clear every one before review (GATE-003).
>   - Rows that rest on the 7 days carry `Assumption = "PRD-12-9"`.
>   - The row log check (I-17) must see neither the invited address nor the token. Add both to the row's secret set if the runner supports it; otherwise extend it.

**AC10 — the invite form in Space settings** (UX: `EXPERIENCE.md:53`, `:484-489`; `mockups/space-settings.html:128-133`; UX-DR27, UX-DR38; ledger `:20`, `:532`)

> - **The section.** A new `Yello.Client/Components/InvitationSection.razor` with `SectionKey.Invitations`. Duplicate sibling keys throw (3.3), so the key must be new.
>   - It renders under a new predicate, `InvitationCapabilities.PermitsIssueInvitations(roleCode) => roleCode is "owner" or "admin"`. Its name fragment must be unique for `ClientRolePredicate` (`RefusalBoundaryGateTests.Spaces.cs:347`).
>   - Gate the predicate against the matrix, as `The_clients_rename_predicate_agrees_with_the_capability_matrix` does.
>   - Add it to `SpaceCapabilities.PermitsSpaceSettings`'s union and to its gate.
> - **The form, following `SpaceNameSection.razor`.** It has:
>   - a `FormField` with `InputType="email"`, a random `FieldName`, and `AutoComplete="off"`. The address is someone else's, so the browser must not offer the user's own; record this as a variance from `Register.razor`'s `email`;
>   - a Role picker as a `fieldset` and `legend` radio group with **no option checked**. It offers the Roles from Q2 and **never Owner**;
>   - a `PrimaryButton`;
>   - a `role="status"` progress line;
>   - **no optimistic update**;
>   - `Writes.SendAsync(HttpMethod.Post, InvitationRoutes.InvitationsOf(spaceId), body, Guid.NewGuid().ToString("D"), ActiveSpace.Token)`. A fresh key is taken per deliberate submit and kept for an identical resend after no answer, as the 2.5 deletes do.
> - **Answers:**
>   - `204`: clear the email field, keep the Role cleared, keep focus on the email field, and announce through `LiveAnnouncement` with copy that names **no address** (UX-DR38).
>   - `already-a-member`: show the refusal beside the field.
>   - `role-not-offerable` and invalid address: show beside the relevant control.
>   - `403`: `RefusalText.Of` (the existing `issue-and-revoke-invitations` copy).
>   - `404`: `OnSpaceRefused`.
>   - Add each new problem type to `RefusalCopy.OwnKeys` and `ClientCopy.resx`, each with a `<comment>` naming 4.1.
> - **Client tests** (`Yello.Tests.Client`, `ClientSurfaceCollection`, `FakeYelloApi`; build `Yello.Client` in **Debug** first). Each runs at a stated viewport:
>   - A Member and a Viewer see no invite control and no settings link. The anchor is an Admin who sees it, and a plant renders it for a Member.
>   - The picker has no Owner option (anchor: three options; plant: add Owner).
>   - No radio is checked at first render.
>   - The recorded request body has exactly `emailAddress` and `role`, plus an `Idempotency-Key`.
>   - The `already-a-member` copy appears beside the field.
>   - The success announcement contains no `@`.
>   - The 320×256 reflow case passes.
>   - Add `TextSpacingTests.Text_spacing_and_text_only_zoom_clip_nothing_in_the_invite_form` under both adaptations (ledger `:20`).

**AC11 — Azure Communication Services and its secret** (ledger `:425`; AR-37; `infra/main.bicep:205-209`)

> - **Bicep.** Replace the named, disabled slot with:
>   - an Email Communication Service with an Azure-managed domain;
>   - a Communication Services resource linked to it;
>   - a Key Vault secret `yello-acs-connection` holding its connection string (written from `listKeys()` within the template, never a parameter literal);
>   - in `infra/modules/containers.bicep`, a container secret reference (`keyVaultUrl` plus `identity`) and an env entry `ConnectionStrings__acs` with `secretRef`.
>
>   The sender address (the managed domain's `DoNotReply@...`) is passed to the app as non-secret configuration, for example `Email__Sender`.
> - **Gates.** `SecretHygieneTests.Every_connection_string_reaches_the_container_as_a_key_vault_secret_reference` must pass with the new entry. Plant: a `value:` instead of `secretRef:` must turn it red. Update `DeploymentShapeTests*` where they enumerate resources, and `docs/operations.md`'s prerequisites (the ACS resource region and its sender domain).
> - **Locally** there is no ACS. The email sender is chosen by configuration (Q4):
>   - with `ConnectionStrings:acs` present, the ACS adapter;
>   - otherwise, in Development only, a file-drop sender that writes each message to a folder git ignores (so 4.3 can be exercised by hand);
>   - in every other environment with no connection string, startup **fails loudly** rather than silently discarding mail.
>
>   Tests replace the sender with a recording fake.
> - **Never deploy.**

**AC12 — Invitations on a Space deletion** (ledger `:689`, story 3.3's C1; FR-7; `SpaceStore.DeleteAsync`)

> - Q1 **[LEE]** decides this. Under the recommended (a), `Invitation → Space` is `ON DELETE CASCADE`. `SpaceStore.DeleteAsync` stays one statement. Its comment and `DeleteSpaceHandler.cs:13` are rewritten to say what is true.
> - **Proof:** extend 3.3's deletion wire test. A Space holding a pending Invitation is deleted, and the Invitation is gone. The outbox row, if still unsent, is **not** (it has no Space reference). Record this. An already-enqueued mail for a deleted Space may still be delivered, and its link will answer 4.3's uniform "no longer valid".
> - **The issuer reference** is `IssuedByAccountId`, a foreign key to `Account` with `NO ACTION`/`Restrict` (Account deletion tombstones, story 5.4; FR-12). There is **no** foreign key to `Membership`. A composite `(SpaceId, MembershipId)` with `SET NULL` would null `SpaceId` too, and `NO ACTION` would block 4.5's removals. Record `IssuedByAccountId` as a deliberate non-Membership reference, so `Every_reference_between_Space_scoped_entities_carries_SpaceId` does not apply: `Account` is not Space-scoped.
> - Also store the issuer's display name at issue (`IssuerName`, max 128). It is the attribution captured at authoring time (`epics.md:1793`), which 4.2's list will show without a live Account lookup.

**AC13 — a live pending Invitation for the same address is refused** (C7; disclosable for the same reason as AC4)

> - Q12 (a) applies. If the active Space holds an Invitation for the same `NormalizedEmailAddress` under the shared **actionable** predicate, the issue is refused `409` `urn:yello:problem:invitation-pending`. The issuer will see that Invitation in 4.2's list, so nothing is disclosed.
> - An **expired** pending Invitation does not block. FR-39: "must be reissued".
> - Do **not** use a filtered unique index `WHERE State = 'Pending'`. Expiry is derived, so an expired row is still `State = Pending` and the index would block the reissue FR-39 requires.

## Tasks / Subtasks

- [ ] **Task 1: decide the open questions** (all ACs)
  - [ ] Read the Dev Notes, then the Open questions. Decide each one as a reversible decision and record it in Completion Notes as "Qn - (x)", with its cost. Each recommendation is the default, and no human is in the loop, so do not stop to ask.
  - [ ] **Exception: questions marked [LEE].** Do not take a [LEE] option without a recorded decision from Lee. Where the **recommended** option of a [LEE] question is itself the variance (Q1, Q6), the orchestrator routes it to Lee before dev. If no decision has arrived, build the recommended option, record it as provisional and pending Lee, and keep it expressible as a single later change (one FK action; one table's shape).
- [ ] **Task 2: Domain** (AC1, AC3, AC8)
  - [ ] Add `Yello.Domain/Invitations/Invitation.cs`. Fields:
    - `Id`;
    - `SpaceId` (init);
    - `EmailAddress`;
    - `NormalizedEmailAddress`;
    - `Role` (init);
    - `State`;
    - `ExpiresAt` (`DateTimeOffset`);
    - `TokenHash` (`byte[]` 32);
    - `IssuedByAccountId`;
    - `IssuerName`;
    - `CreatedAt`.

    Also add `InvitationState { Pending, Accepted, Revoked }`.
  - [ ] Add `Yello.Domain/Accounts/EmailAddressRule.cs`, extracted from `RegisterAccountValidator`. Point registration at it, and keep 1.3's tests green.
  - [ ] Add `Invitation` to `RefusalBoundaryGateTests.SpaceScopedEntities` (`:126`).
- [ ] **Task 3: the Invitation token** (AC7; C8)
  - [ ] The token is 256 random bits from `RandomNumberGenerator`, Base64Url, behind a prefix distinct from the API Token's. Store only `SHA256(token)` in `Invitation.TokenHash`, under a unique index. Mirror `Yello.Infrastructure/ApiTokens/ApiTokenSecret.cs`.
  - [ ] The plaintext exists only in the rendered outbox body until it is sent.
  - [ ] Mirror the two gates:
    - `A_Token_secret_is_256_random_bits_behind_a_prefix_and_is_stored_as_its_SHA_256`;
    - `Only_the_Token_store_and_its_configuration_read_a_Token_hash`, for the Invitation hash.
  - [ ] No contract carries the token. `ApiTokenGateTests.Only_the_issuance_response_carries_a_Token_secret` stays green unchanged.
- [ ] **Task 4: persistence** (AC1, AC3, AC6, AC8, AC12)
  - [ ] `InvitationConfiguration(this)`:
    - query filter `SpaceId == context.ScopedSpaceId`;
    - `HasAlternateKey(SpaceId, Id)`;
    - FK to Space per Q1;
    - FK to Account `Restrict`;
    - `ValueGeneratedNever` on `Id`;
    - `Role` and `State` as strings;
    - `NormalizedEmailAddress` with `BIN2`;
    - index `(SpaceId, NormalizedEmailAddress)` for AC4/AC13;
    - unique index on `TokenHash`;
    - `ExpiresAt` default per Q10.

    Register it in `OnModelCreating`.
  - [ ] `OutboxMessageConfiguration` (Q6), policy-free, with no Space column.
  - [ ] Add `SchemaNames.Invitation` and `SchemaNames.OutboxMessage`. Add Invitation to `SpaceScopedTables`.
  - [ ] Migration `<timestamp>_Invitations`:
    - both tables;
    - `ALTER SECURITY POLICY dbo.SpaceIsolationPolicy ADD FILTER PREDICATE ... ON dbo.[Invitation], ADD BLOCK PREDICATE ... AFTER INSERT, ADD BLOCK PREDICATE ... AFTER UPDATE`. The template is `20261025175001_LabelTables.cs:105-129`. Put Invitation in `TablesThatCannotMove` in `SpaceIsolationSchemaTests`;
    - `Down` drops the predicates before the tables;
    - hand-edit for `IDE0005`, `IDE0161`, `MA0197`, `CA1861` and `S138`. Never add `// <auto-generated />` (`TESTING-CONVENTIONS.md:494-512`). It is expand-only.
  - [ ] `InvitationStore`. The order inside the unit of work is:
    1. take `ApplicationLocks.InvitationsOf(spaceId)` (`Yello/Invitations/{spaceId:D}`);
    2. run the AC4 Membership-join read;
    3. run the AC13 actionable-pending read (shared predicate);
    4. insert with `TokenHash`, `IssuerName` and `ExpiresAt` from the database clock;
    5. call `IOutbox.EnqueueAsync`;
    6. `SaveChanges`.

    No `HOLDLOCK` anywhere (3.4 review). Register it in `SpaceContentStores.cs`, and watch S1200.
  - [ ] Seed helpers in `tests/Yello.Tests.Shared/WireDatabase.cs`: `AddInvitationAsync(spaceId, address, role, expiresAt)` for the expired-row case (R12), and `OutboxRowsAsync()`.
- [ ] **Task 5: the slice** (AC1-AC5, AC13)
  - [ ] Add `Yello.Application/Invitations/IssueInvitation/IssueInvitationHandler.cs` (the FR imperative; `ARCHITECTURE-SPINE.md:274`, `:418`).
    - Signature: `HandleAsync(ActiveSpaceContext, string emailAddress, Role role, CancellationToken)`, returning `IssueInvitationResult(Outcome)`.
    - Outcomes: `Issued`, `AlreadyAMember`, `AlreadyPending`.
    - It never reads `context.Role`. That is gated: Role checks are the pipeline's.
    - The Role ceiling for an Admin is Q2's.
  - [ ] Ports: `IInvitationStore` and `IOutbox` (Application), and `IEmailSender` (Application, consumed only by the Infrastructure dispatcher).
  - [ ] Add `Yello.Host/Endpoints/InvitationEndpoints.cs` with `MapIssueInvitation` and `.RequiresCapability(Capability.IssueAndRevokeInvitations)`.
    - The body is read with `JsonRequestBody.ReadAsync`.
    - Validate the address with the Domain rule and the Role with `StableCode` parsing, refusing Owner.
    - Success returns `Results.NoContent()`.
    - Register the handler in `Program.cs:100-126` and the route at `:352-461`.
  - [ ] `Yello.Contracts`: `Invitations/InvitationRoutes.cs`, `Invitations/IssueInvitationRequest.cs`, and the new `ProblemTypes` (`already-a-member`, `invitation-pending`, `role-not-offerable`, and the address one if no existing validation type fits). Update the AD-19 contract snapshot.
- [ ] **Task 6: the outbox and email** (AC6, AC7, AC11; Q4, Q6, Q7)
  - [ ] Code layout: `Yello.Infrastructure/Outbox/` (`OutboxStore`, `OutboxDispatcher`, `OutboxSweepSchedule`) and `Yello.Infrastructure/Email/` (`AcsEmailSender` over `HttpClient` with HMAC-SHA256 signing, `FileDropEmailSender` for Development).
  - [ ] The Host has a middleware, `OutboxRecoveryTrigger`, modelled on `RefusalRetentionTrigger`, and a process-start kick on `ApplicationStarted`.
  - [ ] The post-commit signal, as AC6 says.
  - [ ] `InvitationEmail`, the template that renders subject and body from `(SpaceName, RoleName, IssuerName, link, expiresAt)` and **only** those. Its parameter list is the content contract. Add a gate that the template type takes no other input, plus AC7's test.
  - [ ] Log messages carry `SpaceId` and the outbox `Id` only. Never the recipient, subject, body or token (`No_log_message_takes_a_name` and I-17).
- [ ] **Task 7: client** (AC1, AC2, AC10)
  - [ ] `InvitationSection.razor`, `InvitationCapabilities.cs`, the client email rule mirror, `SectionKey.Invitations`, `SpaceSettings.razor`, `RefusalCopy.OwnKeys`, `ClientCopy.resx` (Q9 copy, sentence case), and CSS in `components.css` only (logical properties, no px or colour literals).
  - [ ] Extend `FakeYelloApi` for the new route.
- [ ] **Task 8: infra** (AC11; ledger `:425`)
  - [ ] Change `infra/main.bicep`, `infra/modules/containers.bicep`, `docs/operations.md`, `SecretHygieneTests` (unchanged rule, and the new entry passes) and `DeploymentShapeTests*`. Do not deploy.
- [ ] **Task 9: tests and plants** (all ACs). See Testing requirements. Save each original in memory and restore it in a `finally`. **Never** use `git checkout --`, `git restore` or `git reset`. Record `| Plant | Expected red | Seen |` in the Debug Log.
- [ ] **Task 10: ledger, docs, verification**
  - [ ] `deferred-work.md`: apply the Ledger dispositions below. Keep each CLOSED note on one line, introduce no TAB or FORM FEED, and keep the final newline.
  - [ ] Rewrite any comment that predicts 4.1 so it says what exists: `grep -rn "4\.1" Yello.* tests infra`, including `DeleteSpaceHandler.cs:13`, `BoundRegistry`'s comments and the Bicep slot comment.
  - [ ] `tests/TESTING-CONVENTIONS.md` gains a short "Outbox and email" section: the recording fake sender, how to assert same-transaction enqueue, seeding an unclaimed row, and no timers.
  - [ ] `sprint-status.yaml`: this story goes to `review` at the end of dev. `epic-4` stays `in-progress`.
  - [ ] Run `docker info --format '{{.ServerVersion}}'`, then `dotnet build Yello.slnx` (0 warnings, 0 errors), then `dotnet build-server shutdown`, then `dotnet test Yello.slnx --no-build --max-parallel-test-modules 1`. Record the totals. The expected skips are the 3 bundle cases plus the explicit tiers. Rerun a failure once, alone, before calling it real.

## Dev Notes

### The shape of the story in one paragraph

An Owner or Admin posts an address and a Role to a new Space-scoped route. The pipeline has already checked Membership and Role. The slice then:

1. takes a Space-named lock;
2. refuses an address already a Member here, or already holding a live pending Invitation here (both disclosable);
3. otherwise inserts a `Pending` Invitation carrying a hashed token, the issuer's Account id and name, and a database-clock `ExpiresAt`;
4. renders one email into a new outbox row in the same transaction;
5. answers `204`.

After commit, the in-process dispatcher sends the row and deletes it. A crash leaves the row for the process-start sweep, or for the piggyback sweep on the next inbound request. No timer exists. The response never depends on whether the address has an Account. The client gains an invite form in Space settings for Owners and Admins only, with no Owner option and no default Role.

### What exists today (survey at `03c457b`)

**Nothing for Invitations, the outbox, email or ACS exists** beyond the capability enum and matrix row, the bound registry entry (`BoundOperation.AcceptInvitation`, which is 4.3's), one resx key (`issue-and-revoke-invitations`), and the Bicep slot comment.

- **Domain:**
  - `Role` (`Yello.Domain/Memberships/Role.cs`) is stored as its name.
  - `Capability.IssueAndRevokeInvitations`. Its matrix row is `[Yes, Yes, No, No]`, transcribed again at `RefusalBoundaryGateTests.cs:98`.
  - `Account` (`EmailAddress`, `NormalizedEmailAddress`, `DisplayName` max 128).
  - `EmailAddressNormalisation.Normalise` = `Trim().Normalize(FormC).ToUpperInvariant()`.
  - `StableCode.Of(enum)` gives the kebab-case wire codes.
  - `IIdentifierGenerator` gives sequential ids. The database never generates a key (`No_key_is_generated_by_the_database`).
- **The pipeline** (`Yello.Host/Spaces/ActiveSpaceResolution*.cs`, `ApiRouteGroup.cs`):
  - Filters run RequireAccount → AccountScopedResolution → Antiforgery → NotAvailableFloor → Resolve → CreateSpace.
  - `ServeAsync` does presence bound → idempotency → creation bound → endpoint, then **commits after the endpoint returns**, and rolls back on a failure result (`UnitOfWorkResults.IsFailure`).
- **The write-slice template** is Label define (`DefineLabelHandler`, `LabelStore`, `LabelEndpoints.MapDefineLabel`, `CreateLabelRequest`, `LabelSection.razor`) and Space rename (`ActiveSpaceEndpoints.cs:86-118`, `SpaceNameSection.razor`).
- **Isolation in the database:**
  - `dbo.fn_SpaceIsolationPredicate` and `dbo.SpaceIsolationPolicy` (`20260828164716_InitialSchema.cs:217-243`).
  - The latest migration is `20261030201142_SpaceCreation`.
  - The schema tests are `SpaceIsolationSchemaTests` (Slices) and `SchemaVarianceTests` (Isolation). The latter requires every `*SpaceId` column to be under the policy or named as a variance.
- **Locks:** `ApplicationLocks.cs` (`sp_getapplock`, transaction owner, names carry the Space). The 3.4 rule: a plain read first, `UPDLOCK` without `HOLDLOCK` with `INDEX(1)`, and probes `READCOMMITTEDLOCK, FORCESEEK` on a `(SpaceId, ...)` index.
- **Idempotency:** `IdempotencyStore` (24h, a `READCOMMITTEDLOCK` lookup). It **refuses to save with other changes pending**.
- **The no-timer precedents:** `RefusalRetentionTrigger` (middleware, fire-and-forget, `ObserveAsync`), `RefusalPurgeSchedule` (an `Interlocked` claim), `AccessRefusalPurge` (singleton, own scope, own `YelloDbContext`).
- **Tokens:** `ApiTokenSecret` (prefix + Base64Url(32 random bytes); SHA-256 at rest) and its two gates.
- **The duration oracle precedent:** `RegisterAccountHandler`, which hashes first and unconditionally and returns a uniform `204`. The harness is `DurationIndistinguishability`, with `RegistrationDurationTests` and its permanent plant.
- **Client:**
  - `SpaceSettings.razor`: sections keyed `(SectionKey.X, SpaceId)`, each under its own predicate, with a narrated exit on a Role drop.
  - `SpaceCapabilities` and `LabelCapabilities`.
  - `FormField`, `PrimaryButton`, `LiveAnnouncement`, `RoleChip` and `RoleCopy`.
  - `SpaceWrites.SendAsync`, `RefusalCopy.OwnKeys` and `ClientCopy.resx`.
- **Infra:** `infra/main.bicep:205-209` is the disabled slot. `containers.bicep` holds the secret refs at `:16-18`, `:70-73` and `:105-106`. `SecretHygieneTests:95` is the Key Vault reference gate.

### What must be preserved (regression guard)

- **Registration's oracle closure.** Extracting `EmailAddressRule` must not change registration's work or answer. `RegistrationDurationTests` and its plant stay green.
- **The boundary `404` stays byte-identical**, and no `403` becomes a `404` or the reverse (AD-3). No new boundary problem type.
- **No new key-setting line** (`Exactly_one_production_line_sets_a_session_context_key`). **No dual-key unit of work** outside `SpaceCreationScope` (AD-24). The dispatcher and sweep set **no** session key, which is why the outbox table must be policy-free (Q6).
- **No timer or hosted service** (`:701` gate). No `AddHealthChecks`.
- **Space deletion stays one statement** (`The_only_bulk_writes_of_a_Space_are_the_rename_and_the_deletion_in_SpaceStore`).
- **The Space settings narrated exit** on a Role drop, and 3.4's `SpaceNavigationGateTests` and `ClientStaticStateGateTests`. The new section adds no mutable static and no `AddSingleton` in the client.
- **The EF command log filter** at `Program.cs:66-88`.
- **The `Only_the_issuance_response_carries_a_Token_secret` gate**: no contract property named `Token`, `Secret` or `Plaintext`.

### Locking (RCSI; `TESTING-CONVENTIONS.md:167-180`)

Under RCSI, the reads that decide the insert (AC4, AC13) must not race a concurrent issue of the same address. Take `Yello/Invitations/{spaceId:D}` with `sp_getapplock`, transaction-owned, **before** both reads. The lock is named for the request's Space only, never with a value the caller could not see.

The race test, in Slices beside the store tests:

1. Hold the lock from a test transaction.
2. Fire the issue.
3. `WaitUntilBlockedOnALockAsync` (`LCK_M_%`).
4. Insert a pending Invitation for the same address in the held transaction and commit.
5. Assert that the issue answers `invitation-pending` and that exactly one row exists.

Plant: skip the lock. The race test goes red (two rows).

AC4's Membership-arrival race is 4.3's (acceptance creates Memberships). Note it, and do not build it.

### Open questions (decide in Task 1; each is reversible; record as "Qn - (x)")

1. **[LEE] Invitations on a Space deletion** (ledger `:689`, which names Lee/PM; story 3.3's C1).
   - **(a) Recommended. `ON DELETE CASCADE`.** FR-7 says that deleting a Space destroys "its Projects, Tasks, Memberships and Invitations" (`prd.md:202-211`). FR-10's "rather than being deleted" is read as covering the transition **out of pending** (accept, revoke, expire), not the Space's own destruction. AD-2 requires a non-nullable `SpaceId`, and the gate `Every_Space_scoped_entity_has_a_non_nullable_SpaceId` enforces it, so "keep the row with a nulled Space" is not available without relaxing AD-2. `SpaceStore.DeleteAsync` stays one statement.
     - Cost: SM-4 and SM-C3 count only Invitations of Spaces that still exist. Record that as a metric definition note in `success-metrics.md`, or ledger it to Lee.
   - (b) Keep conversion data: in the same deletion transaction, an explicit gated step copies a Space-free tally (kind, state, timestamps; no address, no Space name) into a policy-free `InvitationOutcome` table before the cascade. Cost: a second statement in `SpaceStore.DeleteAsync` (its one-statement gate changes) and a new policy-free table. **Needs Lee.**
   - (c) Nullable `SpaceId` with `SET NULL`. Rejected: it relaxes AD-2 and the non-nullable gate.
2. **[LEE] May an Admin invite at Admin?** (C6; touches who can create an Admin.)
   - **(a) Recommended. Yes. Both issuers may offer Admin, Member and Viewer.** That is exactly what FR-10 and the matrix say: Admin holds "Issue and revoke Invitations", and the only ceiling is Owner (`role-capability-matrix.md:19`, `:42`). FR-13's "only an Owner can promote a Membership to Admin" governs **Role changes of existing Memberships**, and the invite path is a different capability. No matrix change, no gate change, nothing relaxed relative to the written matrix.
     - Cost: an Admin can bring in a new Admin. Lee confirms that this is the intended reading.
   - (b) Narrow it: an Admin may offer Member and Viewer only, mirroring FR-13. This needs a qualified matrix decision (`CapabilityDecision.Qualified`) checked by the pipeline, because slices may not read the Role. It also needs a client picker that varies by Role (absent options, UX-DR27). Cost: a matrix and transcription change plus a new qualification seam. This is a **tightening** and relaxes nothing, but it amends the matrix, which Lee owns.
3. **[LEE] Lookalike addresses** (ledger `:569`, which names Lee/PM before invitations address people by email.)
   - **(a) Recommended. No change: NFC plus `ToUpperInvariant`, as today.** Invitations normalise through the same `EmailAddressNormalisation.Normalise` into the same BIN2 column, so an Invitation matches exactly the Account registration would have matched. Consistency is the property 4.3 needs. Cost: `ＡＢ@example.com` and `AB@example.com` stay distinct people. Record that in the ledger as accepted by default, pending Lee.
   - (b) Fold with NFKC before casing. This changes registration's identity rule, needs a data migration check for existing collisions, and alters 2.6's index semantics. **Needs Lee.**
   - (c) Refuse non-ASCII local parts. A product restriction that contradicts FR-10's "any email address". **Needs Lee.**
4. **The ACS client: a new package or a REST call.**
   - **(a) Recommended. Call the ACS Email REST API with `HttpClient`.** The call is `POST {endpoint}/emails:send?api-version=2023-03-31`, `202` with `Operation-Location`. Authenticate with the HMAC-SHA256 scheme: headers `x-ms-date`, `x-ms-content-sha256`, and `Authorization: HMAC-SHA256 SignedHeaders=x-ms-date;host;x-ms-content-sha256&Signature=...`. The key is parsed from the connection string's `endpoint=...;accesskey=...`. No new NuGet package.
     - **Verify the API version and signing format against Microsoft's current ACS Email REST documentation before writing the adapter**, and pin the version as a constant with a comment citing the doc.
     - Unit-test the signer against a known vector from the docs.
     - Cost: about 100 lines that the SDK would hide.
   - (b) Add `Azure.Communication.Email`. **Needs Lee's approval** (CLAUDE.md, new dependency) and a `PackageVersionPinTests` update. Do not take it without Lee.
5. **The success answer.**
   - **(a) Recommended. `204 No Content`, no body, no `Location`.** The two AC5 arms are then byte-identical with nothing to mask, as registration's uniform `204` is. 4.2's list reads the Invitations itself.
   - (b) `201` with `{id, emailAddress, role, expiresAt}`. Identical in *shape* but not in bytes, so the oracle comparison must mask fields. Rejected: a mask is where a future leak hides.
6. **[LEE] The outbox's shape and isolation** (AD-2 variance; it holds Space-named content outside RLS.)
   - **(a) Recommended. A policy-free `OutboxMessage` table with no Space column**, holding the **rendered** email (recipient, subject, body). The row is deleted on a successful send.
     - This is the only shape that satisfies AC6's process-start sweep without setting a session key: the sweep must see every Space's unsent mail, and `Exactly_one_production_line_sets_a_session_context_key` forbids a second key-setting line.
     - The row holds nothing the email itself does not carry. Only `OutboxStore` and the dispatcher read it; add a named-reader gate in the style of `Space_is_read_only_where_named`.
     - Record it as an AD-2 variance in the spine's style, beside `ApiToken.BoundSpaceId`.
     - Cost: a policy-free table holds a Space name and an address for the seconds or minutes before dispatch.
   - (b) A Space-scoped outbox (`SpaceId`, under the policy). Dispatch on enqueue runs in a fresh unit of work for the same Space through the existing resolver. The "sweep" flushes a Space's backlog only when a request for **that** Space arrives. Relaxes nothing, but it **fails AC6's "runs at process start"**, and mail for a quiet Space waits indefinitely.
   - (c) As (b), plus a policy-free pending index `(MessageId, OriginSpaceId)` for the start-up sweep, which then opens a Space unit of work per Space. Needs a second key-setting path. **Needs Lee.**
7. **What stays after a send** (C5; FS-NFR-2 is 4.6's).
   - **(a) Recommended. Delete the outbox row on a successful send.** Nothing about the message (address, content, token) survives dispatch. 4.6 adds the send record (Space, kind, timestamp; never content or address) and owns SM-C4. Ledger it to 4.6.
   - (b) Redact the row in place (null out recipient, subject and body) and keep it as the send record now. It pulls 4.6's record forward with no Space column, so SM-C4 per Space is not derivable.
8. **Where the invite form lives, given ledger `:532` names 4.2 for the Invitations section.**
   - **(a) Recommended.** 4.1 creates `InvitationSection` with the form only. 4.2 adds the pending list to the same section. AC1's "a Member or Viewer has no invite affordance present" and AC2's "the Role picker ... renders" need a surface in 4.1. Update `:532` to say so.
   - (b) API only in 4.1, with the form in 4.2. Rejected: AC2 then has no picker to test.
9. **The copy** (none is specified; `EXPERIENCE.md:99`: terse, factual, no exclamation marks).
   - **(a) Recommended.** Put these in `ClientCopy.resx`, sentence case:
     - section heading "Invitations";
     - field label "Email address";
     - legend "Role";
     - button "Send invitation";
     - progress "Sending.";
     - success announcement "Invitation sent." (no address);
     - `already-a-member`: "This address already belongs to a Member of this Space.";
     - `invitation-pending`: "This address already has a pending invitation.";
     - `role-not-offerable`: "Choose Admin, Member or Viewer.";
     - invalid address: "Enter an email address.";
     - no Role chosen (client-side): "Choose a Role."

     The email copy lives server-side in `InvitationEmail`:
     - subject: "{Issuer} invited you to {Space} on Yello";
     - body: "{Issuer} invited you to join {Space} as {Role}.", then "Open this link to see the invitation: {link}", then "The link expires on {yyyy-MM-dd} (UTC)."

     There is no other text. English only for now; record that.
   - (b) Any equivalent that keeps the rules: no address in announcements, no Space content, no exclamation marks.
10. **How `ExpiresAt` gets the database clock.**
    - **(a) Recommended.** A column default `DATEADD(day, 7, SYSDATETIMEOFFSET())`, with EF `ValueGeneratedOnAdd` and the value read back. The insert names no `ExpiresAt`, so no C# clock is involved. `No_key_is_generated_by_the_database` concerns keys only, so it does not apply. Cost: the 7 is in the migration; name it in a constant comment and a test.
    - (b) Compute it in an explicit `INSERT ... SELECT DATEADD(...)` statement. Cost: raw SQL in the store.
11. **The State vocabulary** (C2).
    - **(a) Recommended. Stored: `Pending`, `Accepted`, `Revoked`.** "Expired" is derived (AD-27). "Lapsed" is not an Invitation state: nothing lapses an Invitation by event, and FR-12 says issuer loss does not. UX-DR18's five-way list is the **response** vocabulary for 4.3, not a stored enum. Record it.
    - (b) Add `Lapsed` now for symmetry with `OwnershipOffer`. Rejected: an unreachable state.
12. **Duplicate pending Invitations** (C7).
    - **(a) Recommended. Refuse `409 invitation-pending`** while an actionable one exists (AC13). An expired one does not block. This is disclosable because the issuer sees the list in 4.2. It also makes AC8's predicate falsifiable in 4.1.
    - (b) Allow duplicates. Each sends an email, and 4.3 must choose which one an acceptance consumes. Rejected.

**[LEE] summary:** Q1, Q2, Q3 and Q6 are marked [LEE].
- **Q1** and **Q3** are routed because the ledger names Lee/PM.
- **Q2** is routed because it decides who can create an Admin.
- **Q6** is routed because its recommended option is a policy-free table holding Space-named content.
- **Q4 (b)** needs Lee only because it adds a dependency.
- None of the recommended options relaxes RLS, a session key or a gate, except Q6 (a)'s recorded variance. Q6 (a) adds no key-setting line and disables no policy.

### Contradictions found (record, do not resolve beyond what is stated)

- **C1. FR-7 against FR-10, AD-27 and SM-4** on deletion. Q1 decides it.
- **C2. The terminal-state vocabulary.**
  - FR-10 says "accepted, revoked or expired".
  - AD-28 says "revoked, accepted or lapsed".
  - UX-DR18 says "revoked, accepted, expired, lapsed and unrecognised".
  - AD-27 forbids storing "expired".

  Q11 decides it.
- **C3. SPEC drift.** `SPEC.md:274` and `acceptance-criteria.md:81` still say that the Ownership Offer is "surfaced in Space settings rather than emailed", which contradicts FR-43 and AD-26. The PRD wins (`SPEC.md:22`). This does not affect 4.1, beyond confirming that the outbox is reused by 5.1.
- **C4. Two folders.** `ARCHITECTURE-SPINE.md:395` shows `Memberships/RevokeInvitation/`, while `:418` lists `Application/Invitations/*`. Use `Application/Invitations/IssueInvitation/` and record it. 4.2 follows the same folder.
- **C5. FS-NFR-2 against "Outbox send records".** FS-NFR-2 (`prd.md:676-677`) keeps a send record with no content or address. `success-metrics.md:48` derives SM-C4 from "Outbox send records", but an outbox row must hold both. Q7 decides it.
- **C6. Admin inviting at Admin** (FR-10 and the matrix against FR-13's spirit). Q2 decides it.
- **C7. No rule** for duplicate pending Invitations, a pending-Invitation bound, or the status of an already-a-Member refusal. The refusal follows AD-26's 409-with-`type` pattern. Q12 covers duplicates. No bound is added: NFR-8 has none, and an unenforced bound is a defect while an invented one is scope creep.
- **C8. Token storage** is unspecified (NFR-6 and AD-6 cover API Tokens only). Task 3 hashes it anyway.
- **C9. 4.3's unauthenticated lookup by token.** The token must be resolved before any Space context exists, against a table under the Space policy. That is 4.3's problem. Possible answers: a policy-free index table under a named variance (the `ApiToken.BoundSpaceId` precedent), or a Data-Protection-sealed token carrying the SpaceId opaquely. Either is **[LEE]**. Ledger it to 4.3 with Lee. 4.1's unique `TokenHash` index serves either.

### Ledger dispositions (recommended; the dev phase writes them in `deferred-work.md`)

- `:425` → **CLOSED by 4.1**, naming the resources, the secret, the env entry, the gate result and its plant.
- `:569` → **DECIDED (provisional) by 4.1, Q3 (a)**: Invitations normalise through the same function and collation. It stays open to **Lee/PM** for NFKC, with severity unchanged.
- `:689` → **CLOSED by 4.1, Q1 (a)** (cascade) once Lee confirms. Until then, mark it **provisional, Lee/PM**, naming the FK and the deletion test.
- `:20` → append "**UPDATED by 4.1:** the invite form" with the test name. It stays open.
- `:532` → **UPDATED by 4.1**: the Invitations section exists with the form (Q8). The remaining owners are 4.2 (the list), 4.4, 5.1 and 6.3.
- **New entries:**
  - C9, the token lookup before Space context. Owner **4.3**, with **Lee** (an isolation variance). Severity medium.
  - The send record, SM-C4. Owner **4.6** (Q7). Severity low.
  - At-least-once delivery: a crash between send and delete resends one email. Owner **Lee**. Severity low.
  - Mail already enqueued for a Space deleted before dispatch is still delivered. Owner **Lee**. Severity low.
  - A-10 (no scheduled writer of `Invitation.State`), if not built in 4.1 (AC8 (iii)). Owner **5.3**. Severity low.
  - The invite email is English only. Owner **UX**. Severity low.
- Give any other deferral an owner and a severity.

### Architecture compliance (must)

- **AD-21 ring.** The ports go in Application, the outbox and email adapters in Infrastructure, and the endpoints and the trigger middleware in Host. `HttpClient` is BCL, so Infrastructure takes no ASP.NET dependency for the adapter. Check `AllowedReferenceEdges.cs` before adding any edge.
- **AR-3.** The slice never reads Role, Membership, bounds or idempotency.
- **AD-2.** Invitation is fully under the policy, with filter plus both block predicates and the EF filter as an independent second layer. The only policy-free addition is Q6's outbox, which has no `SpaceId` column.
- **AD-24.** No new Account-scoped surface. No `AccountId` session key.
- **AD-10.** No timer and no hosted service. Database work happens only on enqueue, at process start, or piggybacked on inbound authenticated traffic.
- **AD-27.** One predicate, translated to SQL, using the database clock. No C# expiry check.
- **AD-23.** No existence branch. Do not even compute one and discard it.
- **Logs.** `SpaceId` and ids only. Never the address, Space name, token or body.
- **No test-only code in production assemblies** (1.9 Q4). The recording sender is a test double registered by the test host.

### Library / framework

There is no new package (Q4 (a)). Use:

- .NET 10.0.11 BCL: `System.Security.Cryptography` (`RandomNumberGenerator`, `SHA256`, `HMACSHA256`), `System.Buffers.Text.Base64Url`, `HttpClient`/`SocketsHttpHandler` (set `PooledConnectionLifetime` on a long-lived instance).
- EF Core 10.0.11: `[DbFunction]` or `HasDbFunction` mapping for `SYSDATETIMEOFFSET()`, `HasDefaultValueSql`, `ExecuteUpdateAsync` and `FromSql` (Infrastructure only, per the `No_production_code_outside_Infrastructure_bypasses_the_query_filters` gate).
- Microsoft.Data.SqlClient 7.0.2, for `sp_getapplock` through `ApplicationLocks`.
- Blazor WebAssembly 10.0.11.
- xunit.v3 4.0.0, Testcontainers 4.6.0, Playwright 1.62.0, ArchUnitNET 0.13.3.

**ACS Email REST** (Q4 (a)): the GA API version `2023-03-31`, endpoint `POST https://{resource}.communication.azure.com/emails:send`, answering `202 Accepted` with `Operation-Location` and `x-ms-request-id`. The body has `senderAddress`, `recipients.to[].address`, `content.subject`, `content.plainText` and optional `content.html`. The HMAC string-to-sign is `"POST\n{path+query}\n{x-ms-date};{host};{x-ms-content-sha256}"`. **Verify all of this against the current Microsoft docs before coding.** It is recorded here from the creator's knowledge, and no live web check was run in this phase.

### Testing requirements

Follow `tests/TESTING-CONVENTIONS.md`:

- Use only these traits: `Suite`, `Priority`, `Requirement` (cite FR-10, FR-39, AR-28/AD-23, AR-31/AD-27, AR-33/AD-10), `Assumption` (`PRD-12-9` for anything resting on the 7 days) and `Tier`.
- Name each test as an underscore sentence. Never use `Task.Delay`. Races hold a lock and wait on `LCK_M_%`.
- **Never a shared email address.** Randomise every address per test (`test-design-qa.md:132`).
- Seed through the product. Use `WireDatabase` only for what no endpoint does: the expired Invitation, the unclaimed outbox row. Clean up by rollback or container, never by delete statements.
- **No new container-backed test class.** Add partials to the existing wire classes (for example `Spaces/ResolveActiveSpace/ActiveSpaceEndpointTests.*.cs`) and rows to `IsolationCases.cs`. Handler unit tests take no database (the `DefineLabelHandlerTests` template).
- **Every absence has an anchor and is seen red under a plant** (`:192`). A plant caught 0/3 is recorded as masked, never deleted.
- **Memory budget (CLAUDE.md).** Never run two `dotnet test` processes at once. Iterate with `--project ... --filter`. A full run uses `--max-parallel-test-modules 1`. Rerun Client failures alone before calling them real.

**Minimum plant table** (each restored in a `finally`; rebuild `Yello.Client` in Debug before each browser plant):

| # | Plant | Expected red |
|---|---|---|
| 1 | The handler skips the outbox and token work when an Account exists | AC5 duration case (the permanent plant) |
| 2 | The response adds an `accountExists` field, or answers `200` for one arm | the AC5 / `ISO-...-NO-ACCOUNT-ORACLE` byte comparison |
| 3 | Enqueue on a separate `SaveChanges` after the commit, or on a separate context | AC6 forced-rollback case (the outbox row survives) |
| 4 | Dispatch fired before the commit, or on a failure status | AC6 rollback case (a send recorded) |
| 5 | A `PeriodicTimer` or `BackgroundService` in the dispatcher | the `:701` timer gate |
| 6 | The piggyback trigger or the start-up kick removed (each in turn) | the matching AC6 recovery case |
| 7 | The email template given the Space's Project names, or the issuer's address | AC7 (I-14) |
| 8 | The predicate rewritten with `clock.GetUtcNow()` | AC8 gate (ii) and the gate (i) restatement |
| 9 | A second `ExpiresAt >` comparison in a store | AC8 gate (i) |
| 10 | The application lock skipped | the AC13 race case (two rows) |
| 11 | `RequiresCapability` removed from the route | the Member and Viewer rows; `Every_spaceId_endpoint_declares_exactly_one_capability` |
| 12 | The Owner option added to the picker; a server accepting `owner` | AC2 client case; AC2 wire case |
| 13 | The invite section rendered for a Member | AC1 client absence case |
| 14 | The announcement includes the address | AC10 announcement case |
| 15 | `ConnectionStrings__acs` given as `value:` | `SecretHygieneTests` |
| 16 | The Invitation predicate dropped from the security policy | `SpaceIsolationSchemaTests`, `ISO-ISSUE-INVITATION-FOREIGN-SPACE` with the EF filter dropped too (the falsifiable-layer rule) |
| 17 | The invited address logged | the I-17 row log check |

**Run** (CLAUDE.md):

1. `docker info --format '{{.ServerVersion}}'`.
2. `dotnet build Yello.slnx` (0/0).
3. `dotnet build-server shutdown`.
4. `dotnet test Yello.slnx --no-build --max-parallel-test-modules 1`.

### Previous story intelligence (3.4, 3.3, 3.1, 1.3)

- **3.4:**
  - The HOLDLOCK on a vanished row range-locked another Space's key (`c05b8ff`). There is no HOLDLOCK anywhere now.
  - `TokenOwnOtherSpace` is a required row vector for every new Token-reachable route.
  - `IsolationCase.Assumption` exists.
  - The client static-state and navigation gates exist.
  - Absences must be seen red.
- **3.3:**
  - Post-commit side effects go in `response.OnStarting` with a status check, because the pipeline commits after the endpoint.
  - S1200 (too many dependencies) forced type splits.
  - The copy gates rejected Razor `else` and `@if (!(...))` around user-visible text. Use two `@if`s or helper predicates.
  - Duplicate `@key` among siblings throws.
- **3.1:**
  - The dual-key unit of work is exclusive to `SpaceCreationScope`.
  - The section-and-predicate pattern, and the settings union gate.
- **1.3:** the oracle closure. Do the same work for both arms, answer a uniform `204`, and keep the EF command log at Warning.

### Git intelligence

Recent commits:

- `03c457b` Close epic 3
- `c05b8ff` (3.4 review)
- `d9acd8c` (3.4 implementation: 28 files; locks, client gates, isolation rows)
- `e4cdd95` (3.4 ATDD)
- `9077806` (Lee's C1 decision recorded in the story before ATDD)

The pattern is story, then ATDD scaffolds (`[Fact(Skip = "red: ACn - ...")]` plus `atdd-checklist-<key>.md`, rows with `PendingUntil`), then implementation, then review fixes. The orchestrator commits each one. Lee's decisions on [LEE] questions are recorded in the story file before ATDD, as `9077806` did.

### Project Structure Notes

- **New (expected):**
  - `Yello.Domain/Invitations/Invitation.cs`, `InvitationState.cs`
  - `Yello.Domain/Accounts/EmailAddressRule.cs`
  - `Yello.Application/Invitations/IssueInvitation/IssueInvitationHandler.cs`
  - `Yello.Application/Invitations/IInvitationStore.cs`
  - `Yello.Application/Outbox/IOutbox.cs`, `IEmailSender.cs` (or under `Notifications/`; record the choice)
  - `Yello.Infrastructure/Persistence/InvitationStore.cs`
  - `Yello.Infrastructure/Persistence/Configurations/InvitationConfiguration.cs`, `OutboxMessageConfiguration.cs`
  - `Yello.Infrastructure/Persistence/Migrations/<ts>_Invitations.cs`
  - `Yello.Infrastructure/Invitations/InvitationTokenSecret.cs`
  - `Yello.Infrastructure/Outbox/*`, `Yello.Infrastructure/Email/*` (`ARCHITECTURE-SPINE.md:426`)
  - `Yello.Host/Endpoints/InvitationEndpoints.cs`
  - `Yello.Host/Outbox/OutboxRecoveryTrigger.cs`
  - `Yello.Contracts/Invitations/*`
  - `Yello.Client/Components/InvitationSection.razor`
  - `Yello.Client/Spaces/InvitationCapabilities.cs`
  - the client email-rule mirror
- **Updated (expected):**
  - `RegisterAccountValidator.cs`
  - `YelloDbContext.cs`, `SchemaNames.cs`, `ApplicationLocks.cs`, `SpaceContentStores.cs`/`InfrastructureServices.cs`
  - `Program.cs` (handler, route, sender selection, trigger, start kick)
  - `ProblemTypes.cs`
  - `SpaceSettings.razor`, `SpaceCapabilities.cs`, `RefusalCopy.cs`, `ClientCopy.resx`, `components.css`
  - `DeleteSpaceHandler.cs` (comment)
  - `infra/main.bicep`, `infra/modules/containers.bicep`, `docs/operations.md`
  - Tests:
    - `RefusalBoundaryGateTests*.cs` (entities, predicate union, new gates)
    - `SpaceIsolationSchemaTests.cs`
    - `IsolationCases.cs`
    - `WireDatabase.cs`
    - `FakeYelloApi*.cs`
    - `TextSpacingTests.cs`
    - `SecretHygieneTests` / `DeploymentShapeTests*`
    - the contract snapshot
  - `tests/TESTING-CONVENTIONS.md`, `deferred-work.md`
- **No new project and no new package** (Q4 (a)). **No new container-backed test class.**

### References

- `epics.md`:
  - `:94` (FR-10), `:148` (FR-39), `:167` (FS-NFR-2), `:178` (§6.1)
  - `:213-225` (AR-25, AR-28, AR-31, AR-33, AR-37)
  - `:252`, `:256`, `:271`, `:278` (UX-DR14, 18, 27, 34)
  - `:369` (coverage), `:449-457` (epic 4), `:1613-1663` (4.1), `:1665-1753` (4.2, 4.3 for boundaries)
- `prd.md`:
  - `:53-74` (glossary)
  - `:202-211` (FR-7), `:258-301` (FR-10 to FR-13), `:336-352` (matrix)
  - `:643-652` (FR-39), `:676-677`, `:687`, `:719-734` (NFR-1, 5, 6, 7), `:755`
  - `:763-765` (§6.1), `:862-871` (SM-4, SM-C3, SM-C4), `:900` (§12 #9)
- `ARCHITECTURE-SPINE.md`:
  - `:42`, `:71-112` (AD-1 to AD-6), `:132-136` (AD-10), `:182-187` (AD-18)
  - `:195-199` (AD-20), `:207-230` (AD-22 to AD-24), `:232-236` (AD-25), `:238-255` (AD-26 to AD-28)
  - `:274`, `:304`, `:395`, `:418`, `:426`
- `EXPERIENCE.md`: `:53`, `:85`, `:99`, `:115`, `:147`, `:151-155`, `:176-178`, `:211`, `:264-265`, `:370-390`, `:484-489`. `DESIGN.md`: `:201-262`, `:473`, `:497-501`. `mockups/space-settings.html:121-178`.
- `specs/spec-yello/`: `SPEC.md:76`, `:273-274`; `role-capability-matrix.md:19`, `:42`; `decisions-settled.md:29-30`; `success-metrics.md:24`, `:48`; `acceptance-criteria.md:106-114`, `:387-391`.
- `test-design-qa.md`: `:132`, `:165`, `:198`, `:265-278` (I-1, I-2, I-7, I-13, I-14), `:302-308` (A-4, A-5, A-9, A-10), `:317`, `:363-389`. `test-design-architecture.md`: `:212`, `:220`, `:237`, `:319-327`, `:424-439`.
- `tests/TESTING-CONVENTIONS.md`: `:30`, `:49-73`, `:89-122`, `:151`, `:167-197`, `:260-351`, `:458-512`, `:527-568`, `:677-763`, `:823-845`.
- `implementation-readiness-report-2026-08-22.md`: `:399-400`, `:489`, `:518`, `:649`, `:998`, `:1048`, `:1240-1247`, `:1372-1376`.
- `deferred-work.md`: `:20`, `:213`, `:277`, `:425`, `:532`, `:569`, `:670`, `:689`, `:691`.

## Dev Agent Record

### Agent Model Used

### Debug Log References

### Completion Notes List

- Ultimate context engine analysis completed - comprehensive developer guide created (create-story, 2026-10-31).

### File List
