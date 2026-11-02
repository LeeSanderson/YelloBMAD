---
baseline_commit: 17191e8d4ffdd836929599ea2aef549d89cee9b2
---

# Story 4.3: Accept an Invitation

Status: review

Epic: 4 — Bring people into one piece of your work (the third story of the epic)
Story key: `4-3-accept-an-invitation`

**Requirements owned.** From the Story Coverage Index (`epics.md:370`, with the readiness report's additions at `implementation-readiness-report-2026-08-22.md:1416`): **FR-11**, AR-30 / NFR-8 (Memberships per Space), UX-DR18, UX-DR35.

- **FR-11** (`prd.md:271-282`). Every consequence binds:
  - `:276` "Accepting creates exactly one Membership, in exactly the invited Space, at exactly the invited Role."
  - `:277` "The Invitation token identifies the offer; it never authorises acceptance. Acceptance requires the invitee authenticated as the Account the Invitation addresses. A bare fetch of the acceptance route ... creates no Membership and changes nothing."
  - `:278` "Acceptance is a deliberate act, taken after the Space and the Role have been shown. For an invitee with no Account, completing registration is that act; for an invitee who already has one, it is an explicit confirmation. Neither is satisfied by loading a URL."
  - `:279` "An invitee without an Account registers as part of accepting, and that registration provisions their own Personal Space (FR-4) independently of the Space they were invited to."
  - `:280` "An invitee with an existing Account joins with that Account; no second Account is created, and their other Memberships are neither visible to nor affected by the inviter."
  - `:281` "An Invitation can be accepted once. A second attempt with the same Invitation is refused."
  - `:282` "An Invitation that has been revoked (FR-12) reports only that it is no longer valid."
- **AR-30 / AD-25 / NFR-8** (`ARCHITECTURE-SPINE.md:232-236`; `prd.md:743`). Memberships per Space is 100, declared in `BoundRegistry` with `BoundOperation.AcceptInvitation`, checked by the pipeline, refused inside the creating transaction with a machine-readable reason.
- **UX-DR18** (`epics.md:256`; `EXPERIENCE.md:177-178`, `:219`, `:264-265`). The invitation view: a side-effect-free read; Space, Role and issuer; fixed `<title>` "Yello"; no `og:`/`twitter:` metadata; `noindex`; only the opaque token in the URL; one response for revoked, accepted, expired, lapsed and unrecognised, identical in words, shape and duration; the wrong-Account case without echoing either address.
- **UX-DR35** (`EXPERIENCE.md:276`). "This Space has 100 Memberships, the maximum." The copy already exists (`ClientCopy.resx` key `memberships-per-space`; `RefusalCopyTests.A_role_refusal_names_the_capability_and_a_bound_refusal_states_its_number`).

**Also binding, though the coverage row omits them:**

- **AD-28 / AR-32** (`ARCHITECTURE-SPINE.md:251-255`). Presenting is a safe read; Membership only by "a separate explicit state-changing request, authorised on the authenticated Account matching the address the Invitation names"; the transition is guarded `WHERE State = Pending` with a rowcount check; a revoked, accepted or lapsed Invitation "reports only that it is no longer valid".
- **AD-22 / AR-27** (`:207-211`). "Exactly one slice creates an Account ... `AcceptInvitation` delegates to it and never provisions independently; the invited Space's Membership is a separate, additional Membership."
- **AD-23 / AR-28** (`:213-217`). Registration stays identical in status, body, shape and duration for a known and an unknown address. No endpoint returns an email address to anyone but an Owner or Admin of a shared Space.
- **AD-27 / AR-31** (`:245-249`). Acceptance composes `OfferPredicates.Actionable` into its own guarded statement, against `SYSDATETIMEOFFSET()`. Nothing writes "expired".
- **AD-2 / AD-24** (`:77-86`, `:219-230`). The session key is set per unit of work and never from a client-supplied value; only `SpaceCreationScope` sets both keys; a later story needing a new unit-of-work shape amends the spine first. See Q1-Q3 [LEE].
- **AD-18** (`:182-187`). The accept is a state-changing request and honours an `Idempotency-Key` (Q11).
- **AD-9** (`:130`). `MembershipChanged` on any Membership mutation. No publisher exists yet (Q14).
- **NFR-6 / I-17.** The token and the invited address are secrets in logs.
- **NFR-9** (`prd.md:755`). The invitation flow meets WCAG 2.1 AA.
- **§7** (`prd.md:802`). The unauthenticated surface is "register, sign in, accept an Invitation" — read as: the **view** is unauthenticated; acceptance needs a Session (C6).

**Ledger entries owned (`deferred-work.md`). Close each one, or re-own it with a reason:**

| Line | Entry | Severity | Owner per ledger | Covered by |
|---|---|---|---|---|
| `:718` | **C9**: resolving an Invitation by its token before any Space context exists | medium | 4.3 with **Lee** | Q1 **[LEE]**, AC1, AC9 |
| `:213-221` | 4.3 cannot write the invited Space's Membership on registration's unit of work (`@read_only = 1`, Membership BLOCK AFTER INSERT; confirmed by `SpaceIsolationLayerTests`) | medium | 4.3 (AD-24 amendment) | Q3 **[LEE]**, AC3, C1 |
| `:277` | Registration narrows the session context but not `SpaceScope` | low | 4.3 | Q3 (a) leaves registration untouched; the acceptance unit narrows `SpaceScope` (Task 4) |
| `:670` | The both-keys unit of work's contents are not gated; "4.3 meets the same question; add a dependency allow-list gate" | low | 4.3 | Q2, AC12 (allow-list gate on the acceptance unit of work) |
| `:725` | A Membership arriving between an issue's Membership read and its insert; "4.3 decides whether acceptance takes the same lock" | low | 4.3 | Q8, AC5 |
| `:736` | The acceptance route must answer a revoked Invitation with the uniform "This invitation is no longer valid." | low | 4.3 | AC6 |
| `:723` | A-10: `Only_the_Invitation_store_writes_an_Invitation_state` — "4.3 adds its accept to the gate" | low | 5.3 (4.3 named) | AC12 |
| `:743` | The Invitation IL gates see IL calls only; "4.3 should either widen the gates or state their reach" | low | 4.3 | Q18, AC12 |
| `:722` | Mail for a Space deleted before dispatch: "its link answers 4.3's uniform 'no longer valid'" | low | Lee (4.3 named) | AC6 (the unrecognised arm covers a cascaded Invitation) |
| `:36` | AC9 radius mapping: "the invitation view (Epic 4)" takes 6px | low | each owning story | AC2, Task 7 |
| `:20` | 1.4.12 text spacing / 200% text-only zoom: each new surface adds its case | medium | open, rolling | AC2, Task 7 |

**Depends on** (all done):

- 4.1: `Invitation` under RLS, `InvitationTokenSecret` (SHA-256 at rest, unique `UX_Invitation_TokenHash`), `InvitationLinks` (`{ClientOrigin}/invitations/{token}`), `OfferPredicates.Actionable`, `DatabaseClock`, `ApplicationLocks.TakeInvitationsAsync`, the outbox, `MailDrop`, `InvitationWire`, the duration harness for I-7.
- 4.2: the guarded `ExecuteUpdateAsync` revoke and its race test (the acceptance is the other side of that race), `WireDatabase.SetInvitationStateAsync`, the state-writer gate.
- 1.3: `RegisterAccountHandler` (the one Account-creating slice), `RegistrationDurationTests`.
- 1.6: the pipeline, `BoundRegistry`, `BoundaryProblems.BoundReached`, refusal recording, idempotency.
- 1.8: the `ApiToken.BoundSpaceId` policy-free variance (Q1's precedent).
- 3.1: `SpaceCreationScope`, the AD-24 amendment pattern, the creation step in the pipeline (`ActiveSpaceResolution.Creation.cs`) — the closest structural template for a non-`{spaceId}` unit of work.
- 3.4: `TokenOwnOtherSpace`, the client static-state and navigation gates.

**Explicitly not in this story:**

- **The Membership list, Role change, removal** (4.4, 4.5). 4.3 creates Memberships; it shows none.
- **`MembershipChanged` publishing** (7.7 builds the publisher; Q14).
- **Email-address verification at registration.** No requirement asks for it; Q3 records what the address binding rests on.
- **A rate limit on issue** (D3, owner 4.6, Lee/PM). Unchanged.
- **No commit, push, branch, Azure login or deployment.** The orchestrator commits.
- **No new NuGet package and no new project without Lee** (CLAUDE.md).

## Story

As someone who has been invited,
I want to see what I am being offered and then deliberately accept it,
So that I join exactly one Space at exactly one Role, and nothing joins me to anything by accident.

## Acceptance Criteria

AC1 to AC9 are reproduced **verbatim** from `epics.md:1711-1753` (`status: final`) and numbered by position. AC10 to AC13 come from the spines, the ledger and a survey of the code. Each cites its source. Indented notes are this story's implementation guidance; where a note depends on an open question it names it, and the [LEE] questions must be decided before development starts.

**AC1 — the link is a safe, side-effect-free read** (FR-11 `:277`, AD-28)
**Given** an Invitation link
**When** it is fetched
**Then** the response is a safe, side-effect-free read that creates nothing and changes nothing
**And** a mail security scanner, a link prefetcher, or anyone the mail was forwarded to therefore creates no Membership

> - **Two fetches exist.** The browser fetch of `{ClientOrigin}/invitations/{token}` returns the static client shell (`index.html`, which carries no Space data). The client then calls the view API with the token. Both are reads.
> - **The view API** (route shape: Q4 **[LEE]**) resolves the token (Q1 **[LEE]**), sets the Space key on its own read-only unit of work (Q2 **[LEE]**), and reads the Invitation **through `OfferPredicates.Actionable`** plus the Space's name. It writes nothing: no `Invitation` column, no `Membership`, no `AccessRefusal`, no outbox row, no idempotency record. It commits nothing (roll the read transaction back, or use one with no writes).
> - **Proof (wire, Slices).** Fetch the view three times for a pending Invitation, then assert the row is byte-identical (`State = Pending`, every column unchanged), the Space's `Membership` count is unchanged, and no outbox row exists for the invited address. Anchor: the same Invitation is accepted afterwards (AC4) and only then does the count change. Plant: a view that sets `State = Accepted` turns it red.
> - **The view is not a credential check.** It answers the same for no Session, for a Session of any Account, and for a bearer Token (Q7 [LEE]).

**AC2 — the view names Space, Role and issuer, and leaks nothing into previews** (UX-DR18, `DESIGN.md:270-276`, `:475`)
**Given** the Invitation view
**When** it renders
**Then** it names the Space, the Role offered and who issued it, with the most generous padding in the product
**And** no Space name appears in its `<title>` or in any `og:` or `twitter:` metadata, the page is `noindex`, and no Space name or id appears in the URL beyond the opaque token — because a link pasted into a chat tool has its preview scraped into the logs of people with no Membership

> - **The view body** carries exactly: the Space name, the Role code (`admin`, `member`, `viewer`), and `IssuerName` (captured at issue, never a live `Account` lookup — 4.2 Q7 precedent), plus the addressee indicator of Q5 [LEE]. **Never**: the invited address, `SpaceId`, `InvitationId`, `ExpiresAt`, `State`, the issuer's Account id, any count. A contract-shape gate holds the exact field list.
> - **The page** is a new `Yello.Client/Pages/InvitationView.razor` at `@page "/invitations/{Token}"`. It is reachable without a Session: extend `ClientRoutes.IsReachedWithoutASession` (and anything that redirects a session-less visitor to sign-in) so it is not bounced, and no context bar is drawn. Add `ClientRoutes.Invitation(string token)`; the path segment must equal `InvitationLinks.Path` (a gate holds that the client route and the Host's link agree).
> - **Title and metadata.** `<PageTitle>` is the fixed `Yello` (`EXPERIENCE.md:177`). `index.html` keeps no `og:`/`twitter:` tags (a gate reads it). `noindex` must reach a scraper that does not run WebAssembly, so it cannot rest on a `<HeadContent>` meta alone: add an `X-Robots-Tag: noindex` header for `/invitations/*` in `Yello.Client/wwwroot/staticwebapp.config.json` (`routes` with `headers`), and the same header wherever the Host serves the client in Development, **plus** a `<meta name="robots" content="noindex">` via `HeadContent`. Gate the config entry.
> - **Referrer.** The page URL carries the token. Add `Referrer-Policy: no-referrer` for `/invitations/*` in the same places, so no outbound request carries the token in `Referer`. Gate it with the noindex entry.
> - **Design.** Build the `invitation-view` component per `DESIGN.md:270-276`: `surface-card`, hairline border, radius `{rounded.lg}` (6px), padding `{spacing.6}`, title in `{typography.dialog-title}`. "The most generous padding in the product" (`:475`) is a property against every other component: add `DesignFoundationGateTests` cases `The_invitation_view_takes_the_6px_radius` (closes the invitation-view clause of ledger `:36`) and `No_component_declares_more_padding_than_the_invitation_view`. CSS in `components.css` only, logical properties, no px or colour literals.
> - **Copy** (Q15, from `mockups/invitation.html`): heading "{IssuerName} invited you to {SpaceName}."; then "Join as {Role}."; the Role's one-line meaning from `RoleCopy`; the actions (AC3, AC4); the footer for an invitee with no Account: "You'll also get a Space of your own." Uppercase only through CSS (UX-DR42).
> - **Accessibility (NFR-9).** One `<h1>`. Focus starts on the heading region after load. Every state change is announced through `LiveAnnouncement` with no address. 320×256 reflow and the two text-spacing adaptations each get a case (ledger `:20`).

**AC3 — registration from the Invitation is the act of acceptance, through the one registration slice** (FR-11 `:278-279`, AD-22, AD-28; ledger `:213`, `:277`)
**Given** an invitee with no Account
**When** they complete registration from the Invitation
**Then** that registration **is** their act of acceptance, it provisions their own Personal Space in the same transaction independently of the invited Space, and it creates exactly one additional Membership in the invited Space at exactly the invited Role
**And** the slice that creates the Account is the same single slice used by direct registration, never a second provisioning path

> - **The invited Membership cannot be written on registration's unit of work** (ledger `:213-219`, executed by story 1.5): the key is `@read_only = 1` and `Membership`'s BLOCK AFTER INSERT predicate admits only the Personal Space. Both keys would not help either — the BLOCK predicate is Space-only (C1). So the Personal Space provisioning and the invited Membership are **two units of work**, whichever shape Q3 [LEE] takes.
> - **Recommended shape (Q3 (a)):** the view's "Create an Account and join" form posts the existing `POST /api/v1/accounts` **unchanged** (`RegisterAccountHandler`, `AccountRegistrationStore` untouched — AD-22 and AD-23 hold by construction), then signs in with the same credentials, then sends the AC4 accept request with the token. One deliberate act by the person; three requests by the client. The accept is authorised exactly as for an existing Account (AC4).
>   - Partial failure is a valid state: if the accept is refused (bound reached, revoked meanwhile, address mismatch) the person has an Account and a Personal Space (AD-22's "exactly one owned Space" holds) and is told why on the view, with the uniform or bound copy. If the client dies between requests, the Invitation is still pending and the person can sign in and accept from the link.
>   - If the address was already registered, registration answers its uniform `204` and changes nothing (AD-23), and the sign-in then fails with sign-in's ordinary failure copy. That is direct registration's existing behaviour, not a new oracle; record it.
>   - The registration form here asks for display name, email address and password — the same three fields (sprint-change-proposal-2026-09-13 `:90`). **The email field is not pre-filled** with the invited address (UX-DR18: never echo it to a link holder). The accept then succeeds only if the typed address normalises to the Invitation's (AC4).
> - **Proof:** a wire case registers a fresh address equal to the invited one, signs in, accepts, and asserts: exactly one `Account`; exactly two Memberships for it — Owner of a new Space (the Personal Space, named from the display name) and the invited Role in the invited Space; the Invitation `Accepted`. A gate holds that no type but `RegisterAccountHandler` constructs an `Account` (extend whatever already holds AD-22; add it if absent) and that the acceptance slice never references `NewSpace`.
> - **Ledger `:277`** closes under Q3 (a): registration gains no filtered read. Under Q3 (b) the acceptance unit narrows `SpaceScope` itself (Task 4) and the entry closes the same way.

**AC4 — an existing Account accepts by an explicit request, authorised by its own address** (FR-11 `:277-278`, AD-28)
**Given** an invitee who already holds an Account
**When** they accept
**Then** acceptance is an explicit confirmation issued as its own state-changing request, authorised on the authenticated Account matching the address the Invitation names
**And** loading a URL is never sufficient — the token identifies *which* offer is in play and is never the authority for accepting it

> - **The accept route** (shape: Q4 [LEE]) requires a Session (`401` without one, before anti-forgery, recording nothing — the `RequireAccountAsync` precedent), carries the anti-forgery token (AD-7), and runs on a named unit of work (Q2 [LEE]) scoped to the token's Space. A bearer Token is refused `401` (Q7 [LEE]).
> - **The authority is the Account, never the token.** In one guarded statement, under the Space's Invitations lock (Q8):
>
>   ```
>   UPDATE Invitation SET State = 'Accepted'
>   WHERE TokenHash = @hash AND <OfferPredicates.Actionable>
>     AND NormalizedEmailAddress = @callerNormalizedAddress
>   ```
>
>   `@callerNormalizedAddress` is read from the caller's `Account` row (policy-free, by the Session's `AccountId`), never from the request. Then, only on rowcount 1, insert one `Membership(SpaceId = Invitation.SpaceId, AccountId = caller, Role = Invitation.Role)`. Same transaction; the pipeline commits.
>   - `ExecuteUpdateAsync` composed with `.Where(OfferPredicates.Actionable)`, as 4.2's revoke. Never load-check-save.
>   - The Role and Space come from the row, never from the body. The body carries the token and nothing else (a contract-shape gate).
> - **Success answer** (Q10): `200` with `AcceptedInvitationResponse(Guid SpaceId)` — the caller is now a Member, so the id is theirs to know. The client navigates to `ClientRoutes.SpaceHome(spaceId)` through `SpaceNavigation.GoTo` (the 3.4 navigation gate).
> - **"Loading a URL is never sufficient"** is proved by AC1's case plus: a `GET` to the accept route is `405` or the unmatched `404` (never a write); and the client sends the accept only from the button's click handler (client case: navigating to the view sends no accept request; anchor: clicking does).

**AC5 — a second acceptance is refused by the guard, and no second Membership exists** (FR-11 `:281`, AD-28, AD-26's discipline)
**Given** an accepted Invitation
**When** a second acceptance is attempted with it
**Then** it is refused under a guarded `WHERE State = Pending` with a rowcount check, so no second Membership is created

> - The guarded update matches no row; nothing is inserted; the answer is AC6's uniform response (C5: the epics put "accepted" among the five uniform states, which wins over AD-26's 409 on this route). A replay **with the same `Idempotency-Key`** returns the original `200` from the record instead (Q11, AD-18).
> - **Races (deterministic, `TESTING-CONVENTIONS.md:167-180`; `WaitUntilBlockedOnALockAsync`):**
>   1. *Accept vs accept:* hold the Space's Invitations lock on a test connection; fire two accepts for the same token and Account (distinct idempotency keys); wait until both are blocked on `LCK_M_%`; release. Exactly one `200`, one uniform refusal, one Membership.
>   2. *Accept vs revoke* (4.2's race from the other side): a holder updates the row to `Revoked` uncommitted; fire the accept; wait; commit. The accept answers uniform, no Membership. And the mirror: a holder sets `Accepted` uncommitted, fire the revoke — already covered by `A_revoke_racing_an_acceptance_that_commits_under_it_answers_409_and_leaves_it_accepted`; keep it green.
>   3. *Accept vs issue* (ledger `:725`, Q8): hold the Invitations lock; fire an issue to an address and an accept that would make that address a Member; with acceptance taking the same lock, the later of the two sees the earlier (issue answers `already-a-member`, or the accept proceeds and the issue then refuses). Assert no state where the address is both a Member and holds a new pending Invitation.
> - **Plants:** the accept without the `Actionable` guard (race 2 red; the expired and revoked arms of AC6 red); the accept as load-check-save (race 1 or 2 red, or recorded masked); the accept without the lock (race 3 red, or masked with the reason).

**AC6 — five states, one response** (FR-11 `:282`, AD-28, UX-DR18, `EXPERIENCE.md:264`; ledger `:736`, `:722`)
**Given** an Invitation that is revoked, accepted, expired, lapsed or entirely unrecognised
**When** the acceptance route is followed
**Then** one identical response is returned for all five, identical in words, shape and duration: "This invitation is no longer valid."
**And** it discloses neither the Space, its contents, nor who revoked it — the uniformity being the point, since a distinct response for a token that never existed would make the route an existence oracle

> - **"The acceptance route"** is both the view and the accept: both give this response for all five states. Stored states are `Pending`, `Accepted`, `Revoked` only (4.1 Q11); "expired" is `Pending` with `ExpiresAt` past (derived); "lapsed" has no stored form for Invitations and is covered by the same derived arm; "unrecognised" covers a token never issued, a malformed token (wrong prefix, wrong length, not base64url), and a token whose Invitation was cascaded away with its Space (ledger `:722`).
> - **The response** (Q6 [LEE]): one status and one problem body, byte-identical across the five, e.g. `404` with a new `urn:yello:problem:invitation-not-valid`, no `instance`, no id, no Space. Client copy: heading "This invitation is no longer valid.", then "Ask whoever invited you to send a new one.", then a "Go to Yello" link (mockup). Add the type to `ProblemTypes`, `RefusalCopy.OwnKeys` and `ClientCopy.resx` with a `<comment>` naming 4.3.
> - **Identical duration** (Q6 [LEE]): the work must not branch on recognition. An unrecognised token still runs the same statement sequence (Q6 (a): the same Space-key set with a server-generated sentinel id and the same `Invitation` read, which then sees nothing), and the route is held to a fixed minimum by a floor in the `NotAvailableFloor` style. Nothing is recorded as an `AccessRefusal` for any of the five.
> - **Proof.**
>   - Byte equality (Slices, and isolation row `ISO-INVITATION-NOT-VALID-SAME-FOR-FIVE-STATES`): seed one of each — revoked (through 4.2's route), accepted (through this story's accept), expired (`WireDatabase`, the R12 method; `Assumption = PRD-12-9`), unrecognised (a fresh well-formed token never stored), malformed, and Space-deleted — and assert identical status, headers that matter and bodies, for both the view and the accept. Anchor: a live pending Invitation answers differently (the offer).
>   - Duration (a timing case in a `DisableParallelization` collection, `DurationIndistinguishability`, 21 interleaved samples, server-side `CompareMeasuredAsync`): arm A revoked, arm B unrecognised; then arm A expired, arm B unrecognised. Keep a **permanent planted oracle** beside it (`RegistrationDurationTests` model): a variant that returns early for an unrecognised token must fail the comparison. If the oracle is not caught, record the duration proof as masked in the ledger (Lee/TEA, the D4 precedent) — never delete it.
>   - The I-17 log slice holds neither the token nor any address for every arm.

**AC7 — a different Account signed in: stated without echoing either address** (UX-DR18, `EXPERIENCE.md:265`)
**Given** a different Account is signed in when the link is opened
**When** the mismatch is detected
**Then** it is stated without echoing either address, and sign-out is offered
**And** the reason is stated: the helpful version discloses the invited address to whoever holds the link, which on a forwarded link is a stranger

> - **Detection** (Q5 [LEE]): at view time when a Session is present (the view answers an `addressedToYou` boolean, absent for no Session), and always at accept time (the guarded update's address clause; on rowcount 0, a read of whether the token names an actionable Invitation in this Space decides between mismatch and AC6's uniform answer).
> - **Ordering:** a non-actionable Invitation answers AC6's uniform response whoever is signed in. The mismatch is reported only for an actionable one (Q5).
> - **The accept's mismatch answer** (Q13): `403` with a new `urn:yello:problem:invitation-for-another-account`, no address, nothing written, not recorded as an `AccessRefusal` (it is neither of AD-20's kinds, the bound refusal's precedent).
> - **Copy** (mockup): heading "This invitation is for a different Account.", then "Sign out and sign in as the Account it was sent to.", a `button-primary` "Sign out" and a `button-secondary` "Cancel". Sign-out uses the existing sign-out flow (server-side `SessionsValidAfter`, CLAUDE.md) and returns the person to the same invitation URL, where the view re-renders for no Session.
> - **"The reason is stated"**: in the remarks of the view endpoint and of the accept handler, quoting `EXPERIENCE.md:265`.
> - **Proof:** the response bodies (view and accept) for the mismatch contain no `@`, neither address, and no address fragment; the client page contains neither address (Playwright, with both addresses as needles). Anchor: the matching Account sees the offer and accepts. Plant: the copy given the invited address turns the client case red.

**AC8 — no second Account; other Memberships untouched and unseen** (FR-11 `:280`, UJ-2)
**Given** an invitee with an existing Account
**When** they join
**Then** no second Account is created, and their other Memberships are neither visible to nor affected by the inviter

> - **Proof (isolation world).** The invitee owns a populated Space B (randomised Project, Task, Label names) and is a Member of Space C. The inviter (Owner of A) invites the invitee's address; the invitee accepts. Then assert:
>   - one `Account` for the address; Memberships: A (invited Role), B (Owner), C (unchanged Role), nothing else;
>   - the inviter's requests for B and C are the boundary `404` (I-1), and nothing the inviter can read in A (Board, lists, Space settings, the Invitation list) contains a B or C name;
>   - the invitee's B and C rows are byte-identical before and after.
> - Isolation rows: `ISO-ACCEPT-JOINS-ONLY-THE-INVITED-SPACE` and `ISO-INVITER-SEES-NOTHING-OF-THE-INVITEES-OTHER-SPACES`, both surfaces (the inviter's Token bound to A is the ApiToken surface).

**AC9 — the 101st Membership is refused at acceptance, from the bound registry** (AR-30, AD-25, NFR-8, UX-DR35)
**Given** a Space already holding 100 Memberships
**When** acceptance would create the 101st
**Then** it is refused inside the same transaction with a machine-readable reason, from the Story 1.6 bound registry rather than from a check written here
**And** the copy states the bound plainly — "This Space has 100 Memberships, the maximum." — because a bound must degrade visibly, never silently
**And** the refusal lands on **acceptance** rather than on issue, because acceptance is the transaction that creates the Membership; an Invitation issued while the Space had room may be accepted after it filled, so refusing at issue would check the wrong moment and refusing nowhere would breach the bound

> - **The pipeline consults it, not the slice** (AR-3; `ActiveSpaceResolution.Bound.cs`). Declare `.ConsultsBound(Bound.MembershipsPerSpace)` on the accept endpoint, and have the acceptance step call the existing `CreationBound.ConsultAsync` inside the acceptance unit of work. Add the `MembershipsPerSpace` arm to `BoundCounter.CountAsync`: take the Space's lock (Q8), then `SELECT COUNT(*) FROM dbo.[Membership]` (RLS narrows it to the key's Space). Remove the "Memberships per Space is 4.3's" text from the throwing arm.
> - **Order inside the unit (Q9):** lock → token resolves to an actionable Invitation addressed to the caller → bound count → guarded update and insert. A holder of a non-actionable or someone else's token never learns that the Space is full: they get AC6 or AC7 first.
> - **The answer** is the existing `409 bound-reached` with `bound: "memberships-per-space"`, `limit: 100`; the Invitation stays `Pending` (so it can be accepted if a Membership is later removed, while it is still actionable); nothing is written; no `AccessRefusal`.
> - **Issue consults no bound** (4.1's rule, unchanged). Gate: `MapIssueInvitation` declares no `RequiredBound`.
> - **Proof:** seed 99 Memberships of 99 distinct Accounts in the one Space, plus the Owner = 100. The existing `SeedMembershipsAsync` (`ActiveSpaceEndpointTests.CreateSpace.cs:356`, through `SpaceSeeding`) seeds **one Account's** Memberships across Spaces for the Spaces-per-Account bound; add a sibling in `SpaceSeeding` that seeds N Accounts into one Space in one batch (the B2-block style, `TESTING-CONVENTIONS.md:792-810`) rather than 99 HTTP registrations; issue an Invitation (succeeds: issue ignores the bound); accept → `409 bound-reached`, 100 Memberships, Invitation `Pending`. Anchor: at 99 total the same accept succeeds. Race: two invitees accepting at 99, overlapped on the lock — one `200`, one `409`, 100 Memberships. Client case: the bound copy renders on the view. Plant: the count skipped (101 rows; red).

**AC10 — isolation rows on both surfaces** (AD-4; I-1, I-7, I-13, I-17; `TESTING-CONVENTIONS.md:704-811`)

> Both new routes fail `Every_api_route_the_Host_maps_but_the_named_credential_routes_is_named_by_a_case` until they have rows. Add them to `tests/Yello.Tests.Isolation/IsolationCases.Invitations.cs` under "Story 4.3 - view and accept an Invitation", each stating both surfaces:
>
> - `ISO-INVITATION-VIEW-DISCLOSES-ONLY-THE-OFFER`: the body holds the Space name, Role and issuer name of the token's Invitation and none of the Space's Project, Task, Label or Member names, no address, no id (I-14's shape for the view).
> - `ISO-INVITATION-NOT-VALID-SAME-FOR-FIVE-STATES` (I-13): AC6, both routes.
> - `ISO-ACCEPT-INVITATION-NO-SESSION` and the Token surface: `401`, byte-identical, nothing written (Q7).
> - `ISO-ACCEPT-INVITATION-OTHER-ACCOUNT`: AC7's `403`, nothing written, no address in the body.
> - `ISO-ACCEPT-JOINS-ONLY-THE-INVITED-SPACE` and `ISO-INVITER-SEES-NOTHING-OF-THE-INVITEES-OTHER-SPACES`: AC8.
> - `ISO-ACCEPT-INVITATION-TOKEN-FROM-ANOTHER-SPACE`: an Account holding a live Invitation to Space X sends Space Y's token (addressed to someone else): AC7's mismatch or AC6's uniform answer, never a Membership in Y.
> - Rules: every token and address seeded is in the actor's secrets (`HoldsSecret`) for the I-17 log check; seed Invitations through 4.1's issue route and acceptances through this story's route; `WireDatabase` only for the expired row. If ATDD lands rows with `PendingUntil: "4.3"`, clear every one before review (GATE-003).
> - `TESTING-CONVENTIONS.md:767`'s "Story 3.4's `WireDatabase.AddMembershipAsync` was used because Invitations do not exist until Epic 4": leave existing seeds alone; new cases seed Memberships through acceptance where the case is about Membership.

**AC11 — the accept is idempotent under retry** (AD-18, AR-25, P-10; Q11)

> - A replay with the same `Idempotency-Key` after a committed accept returns the original `200` body; no second Membership; the row stays `Accepted`.
> - A **new** key on an accepted Invitation answers AC6's uniform response.
> - A malformed key is `400`, from the pipeline.
> - The record lives where Q11 places it; `Every_Space_scoped_state_changing_endpoint_answers_a_result_the_record_can_replay` (or its Account-scoped sibling) stays green.

**AC12 — gates: one writer of the state, a named unit of work, its contents held** (A-10, AD-2, AD-24, AD-27; ledger `:723`, `:743`, `:670`)

> - **`Only_the_Invitation_store_writes_an_Invitation_state`**: add the accept as the third sanctioned write (insert of `Pending`, revoke, accept), all in `InvitationStore` (or a named sibling store, then name it). Plant: the accept's `State` write moved into another type turns it red.
> - **Ledger `:743` (Q18):** widen the Invitation gates with a source-text scan: no `ExecuteSql*`/`SqlQuery`/`FromSql` string in production code names `Invitation` except the named store(s); state the remaining reach (expression-tree filters inside the store) in the gates' remarks.
> - **`Only_the_Invitation_store_and_its_configuration_read_an_Invitation_token_hash`**: the token resolution reads the hash. Keep the reader set to the configuration, the store, and (under Q1 (a)) the token index's configuration and its one reader.
> - **The session-key setters are named** (Q2): a new gate `The_SpaceId_key_is_set_by_exactly_the_named_units_of_work` lists `MembershipActiveSpaceResolver`, `AccountRegistrationStore`, `SpaceCreationScope` and the new acceptance unit of work. `No_caller_but_the_Space_creation_unit_of_work_sets_both_session_context_keys` stays green unchanged: the acceptance unit sets **only** `SpaceId`.
> - **The acceptance unit's contents (ledger `:670`):** a dependency allow-list gate on the types that run under the acceptance unit of work (the step, the store, the bound counter, the lock helper, the idempotency port), modelled on `Only_the_pipeline_depends_on_the_capability_matrix_and_the_refusal_recorder`. The view's unit of work reads only the Invitation offer fields and the Space name.
> - **`Space_is_read_only_where_named`**: add the one type that reads the invited Space's name for the view, with a comment naming 4.3; its read filters on the token-resolved `SpaceId`.
> - **`Only_the_shared_actionable_predicate_compares_ExpiresAt`** stays green: the view and the accept compose `OfferPredicates.Actionable`. Plant: a view written with its own `ExpiresAt >` turns it red.
> - **`No_production_code_runs_on_a_timer_or_as_a_hosted_service`** stays green.
> - **The AD amendment gate** (Q2): the spine carries a dated amendment for the acceptance unit of work, asserted by phrase in the style of `The_AD_24_amendment_names_Space_creation_and_states_its_reason`.

**AC13 — the client flow** (UX: `EXPERIENCE.md:219`, `:264-265`, `:273`, `:491-499`; `mockups/invitation.html`; UX-DR18, UX-DR27, UX-DR31, UX-DR38, UX-DR39)

> - **States of `InvitationView.razor`:** loading (visually hidden "Loading the invitation."; render nothing Space-named before the server answers); offer (AC2); no-longer-valid (AC6); different Account (AC7); bound reached (AC9, `RefusalCopy`); not sent (no answer or `5xx`: "Not sent." and keep the idempotency key for an identical resend).
> - **No Session:** the offer shows "Create an Account and join" (`button-primary`) and "Already have an Account? Sign in". Sign-in happens **inline on the view** (Q16), reusing the sign-in form's fields and `SessionState`, so the token never leaves the page URL and no return-URL parameter is introduced. After sign-in the view re-reads (the addressee indicator now applies) and shows "Join {Space}" (`button-primary`).
> - **Registration from the view** (Q3 (a)): the three-field form, the in-flight copy and disabled resubmission (`EXPERIENCE.md:273`), then sign-in, then accept, then navigate. Re-fetch the anti-forgery token after sign-in (`TESTING-CONVENTIONS.md:414-456`: a token minted while anonymous is refused once a Session exists).
> - **Session present and matching:** "Join {Space}" sends the accept with a fresh `Idempotency-Key` per deliberate click, kept for an identical resend. Never optimistic: navigation happens after the `200`.
> - **After `200`:** `SpaceNavigation.GoTo(ClientRoutes.SpaceHome(spaceId))`; the switcher re-reads (`SpaceDirectory.Forget`) so the new Space is listed; announce "You joined {Space}." Focus follows the destination's rules.
> - **Gates and rules:** no storage of the token (`ClientStorageBanTests`); no mutable static or singleton (`ClientStaticStateGateTests`); every new `NavigateTo` goes through `SpaceNavigation.GoTo` or is counted in `SpaceNavigationGateTests` with its reason; copy in `ClientCopy.resx` with `<comment>` naming 4.3; no Razor `else` or `@if (!(...))` around user-visible text (3.3 copy gates); the address appears nowhere on this page.
> - **Client tests** (`Yello.Tests.Client`, `ClientSurfaceCollection`, `FakeYelloApi` extended in `FakeYelloApi.Invitations.cs` using `InvitationRoutes`/`ProblemTypes` constants only): the offer renders Space, Role, issuer; `document.title` is `Yello` and no `og:`/`twitter:` meta exists; navigating to the view sends no accept; the accept is sent once with an `Idempotency-Key` and the page waits for the answer (hold → assert → release); the uniform copy for a `404 invitation-not-valid`; the different-Account copy has no address and offers sign-out; the bound copy; registration chains register → sign-in → accept in that order; 320×256 reflow; `TextSpacingTests.Text_spacing_and_text_only_zoom_clip_nothing_in_the_invitation_view` under both adaptations. Build `Yello.Client` in **Debug** first.

## Tasks / Subtasks

- [x] **Task 0: Lee's decisions** (before any code)
  - [x] Q1-Q7 are **[LEE]**. The orchestrator routes them to Lee before development. Record each answer in this file under "Decided by Lee" (the `9077806` precedent) before ATDD. If development starts without an answer, stop and return the question as a blocker; do **not** build an option that adds a policy-free table, a session-key setter, or a token-to-Space resolution without Lee's recorded decision.
- [x] **Task 1: decide the other open questions** (Q8-Q19). Each recommendation is the default. Record each as "Qn - (x)" with its cost in Completion Notes.
- [x] **Task 2: spine amendment** (AC12; Q2)
  - [x] Add a dated amendment to `ARCHITECTURE-SPINE.md` in the form Lee approves (AD-2 and/or AD-24, and AD-28): the acceptance unit of work, the key's source, what it reads, what it writes, and why it is not a third Account-scoped surface. Additive; AD-24's rule line stands. Add the phrase gate.
  - [x] Correct AD-24's 2026-10-29 amendment sentence that names "story 4.3's registration-while-accepting" as needing both keys (C1), by an additive note, not by rewriting history.
- [x] **Task 3: Contracts** (AC2, AC4, AC6, AC7)
  - [x] Extend `Yello.Contracts/Invitations/InvitationRoutes.cs` with the view and accept routes (Q4) and their client helpers.
  - [x] `InvitationOfferResponse(string SpaceName, string Role, string IssuerName, bool? AddressedToYou)` (per Q5) and `AcceptedInvitationResponse(Guid SpaceId)`; the accept request (per Q4) carries the token only. **No property named `Token`, `Secret` or `Plaintext`** (`ApiTokenGateTests.Only_the_issuance_response_carries_a_Token_secret` matches names exactly) — e.g. `InvitationToken`.
  - [x] `ProblemTypes.InvitationNotValid` and `ProblemTypes.InvitationForAnotherAccount`, with remarks on status and disclosure.
- [x] **Task 4: Infrastructure** (AC1, AC4-AC6, AC9)
  - [x] Token resolution per Q1 (a): policy-free `InvitationToken` table (`TokenHash` unique, `InvitationId`, `OfferedSpaceId`), written by `InvitationStore.IssueAsync` in the issue's transaction, foreign key to `Invitation (SpaceId, Id)` with cascade (so a Space deletion still takes it), one named reader; `SchemaVarianceTests.NamedVariances` gains `InvitationToken.OfferedSpaceId` with its reason; a named-reader gate in the `Space_is_read_only_where_named` style. Hand-edited, expand-only migration (`TESTING-CONVENTIONS.md:519-540`); backfill existing Invitations from `Invitation.TokenHash` in the migration. Or Q1 (b) if Lee chooses it.
  - [x] The acceptance unit of work (name e.g. `InvitationScope`, beside `SpaceCreationScope`): begin transaction, `SpaceSessionContext.SetAsync(resolvedSpaceId)`, narrow `SpaceScope` (never `Resolve` — no Role is held), clear on dispose. The view uses it read-only; the accept writes.
  - [x] `InvitationStore`: `ReadOfferAsync` (projection of Space name + Role + IssuerName + address-match boolean, through `Actionable`), `AcceptAsync` (the guarded update + insert; outcomes `Accepted(spaceId) | NotValid | ForAnotherAccount | AlreadyAMember`), both under `ApplicationLocks.TakeInvitationsAsync` for the accept (Q8). Watch S1200; split by port if it fires and record it.
  - [x] `BoundCounter`: the `MembershipsPerSpace` arm.
- [x] **Task 5: Application** (AC4-AC7)
  - [x] `Yello.Application/Invitations/ViewInvitation/ViewInvitationHandler.cs` and `Yello.Application/Invitations/AcceptInvitation/AcceptInvitationHandler.cs` (C8: `Invitations/`, not `Memberships/`). Handlers read no Role and never count (AR-3). The accept handler takes no `IOutbox` and never references `NewSpace` or `RegisterAccountHandler` (gates).
- [x] **Task 6: Host** (AC1, AC4, AC6, AC7, AC9, AC11)
  - [x] A pipeline step for the acceptance unit, in `ActiveSpaceResolution` (the `Creation.cs` model; nested types to stay under S1200): resolve the token, open the unit, idempotency (Q11), the endpoint, bound consultation in Q9's order, commit or roll back. The view step: resolve, open read-only, floor (Q6), answer.
  - [x] Endpoints in `InvitationEndpoints`: explicit outcome switch, **no `_ =>` arm** that turns an unknown outcome into a `404` (4.1 review P10). `Program.cs`: register and map next to the 4.2 routes; update the route comment block.
  - [x] Development-time client hosting: the `X-Robots-Tag` and `Referrer-Policy` headers for `/invitations/*` (AC2), if the Host serves the client.
- [x] **Task 7: Client** (AC2, AC3, AC7, AC9, AC13). `InvitationView.razor`, `ClientRoutes`, `staticwebapp.config.json`, `ClientCopy.resx`, `RefusalCopy.OwnKeys`, `components.css`, `FakeYelloApi.Invitations.cs`.
- [x] **Task 8: tests and plants** (all ACs). See Testing requirements. Save each original in memory and restore it in a `finally`. **Never** `git checkout --`, `git restore` or `git reset`. Record `| Plant | Expected red | Seen |` in the Debug Log.
- [x] **Task 9: ledger, docs, verification**
  - [x] Apply the ledger dispositions below (one line per CLOSED/UPDATED note, no TAB or FORM FEED, keep the final newline).
  - [x] `grep -rn "4\.3" Yello.* tests` and rewrite every comment that predicts 4.3 so it says what exists (at least `AccountRegistrationStore`, `RegisterAccountHandler`, `InvitationState.Accepted`, `Invitation.State`, `OfferPredicates`, `InvitationTokenSecret`, `InvitationLinks`, `BoundRegistry`, `BoundCounter`, `InvitationStore`).
  - [x] `sprint-status.yaml`: this story goes to `review` at the end of dev. `epic-4` stays `in-progress`.
  - [x] `docker info --format '{{.ServerVersion}}'`, `dotnet build Yello.slnx` (0/0), `dotnet build-server shutdown`, `dotnet test Yello.slnx --no-build --max-parallel-test-modules 1`. Record totals. Rerun a failure once, alone, before calling it real.

## Dev Notes

### The shape of the story in one paragraph

The email's link opens a new unauthenticated client page. The page asks a new view endpoint about the token; the Host resolves the token to its Space without any Space context (Q1), opens a read-only unit of work keyed to that Space (Q2), and answers either the offer (Space name, Role, issuer name — nothing else) or one uniform "no longer valid" for every other state, in the same time (Q6). Acceptance is a separate `POST` that needs a Session: under the Space's Invitations lock it flips the Invitation `Pending → Accepted` with one guarded `UPDATE` whose `WHERE` composes the shared actionable predicate and requires the caller's own normalised address, and only then inserts one Membership at the Invitation's Role; the pipeline consults the 100-Membership bound before the write and commits. An invitee without an Account registers through the unchanged registration slice, signs in, and the client sends the same accept (Q3). Nothing is emailed, nothing is deleted, and no address ever appears on the page.

### What exists today (survey at `99bd3d3`)

- **Domain:** `Invitation` (`State` has a public setter; `SpaceId`, `Role`, `TokenHash`, `NormalizedEmailAddress`, `IssuerName`, `ExpiresAt` set at insert); `InvitationState { Pending, Accepted, Revoked }` (no Expired, no Lapsed); `Membership(Id, SpaceId, AccountId, Role, CreatedAt)`; `BoundRegistry` already declares `MembershipsPerSpace = 100, AcceptInvitation`; `EmailAddressNormalisation.Normalise` (NFC + `ToUpperInvariant`; Lee's 4.1 Q3: no NFKC).
- **Application:** `IInvitationStore` (`IssueAsync`, `ListPendingAsync`, `RevokeAsync`), `InvitationLinks.Path = "/invitations/"`, `RegisterAccountHandler` (hashes first, returns nothing — keep it that way), `ISpaceCreationScope`.
- **Infrastructure:**
  - `InvitationStore` (lock → reads → insert; list; guarded revoke). `YelloDbContext` has no `DbSet<Invitation>` (S1200); use `Set<Invitation>()`.
  - `InvitationTokenSecret` (`yello_inv_` + base64url(32 bytes); SHA-256 stored; "nothing encoded in it").
  - `InvitationConfiguration`: unique `UX_Invitation_TokenHash`, index `(SpaceId, NormalizedEmailAddress)`, FILTER + BLOCK AFTER INSERT + BLOCK AFTER UPDATE predicates (migration `20261101140742_Invitations`). The BLOCK AFTER UPDATE admits the accept's update (Space unchanged).
  - `SpaceSessionContext.SetAsync`/`SetAccountAsync` — the one key-setting line; each key `@read_only = 1`.
  - `SpaceCreationScope` (both keys; the only one), `AccountRegistrationStore` (SpaceId key = new Personal Space), `MembershipActiveSpaceResolver` (SpaceId key from the route + Membership).
  - `ApiTokenConfiguration`: the policy-free `ApiToken.BoundSpaceId` precedent (looked up by hash before any Space is known; one reader; named in `SchemaVarianceTests.NamedVariances`).
  - `BoundCounter` (Projects, Tasks, Spaces-per-Account arms; the Memberships arm throws "4.3's").
  - `ApplicationLocks.TakeInvitationsAsync` (`Yello/Invitations/{spaceId}`).
  - Membership's unique alternate key on `(SpaceId, AccountId)` exists (the `ApiToken` composite FK's principal key) — a second Membership for the same pair fails with 2601/2627.
  - `Space` is policy-free; readers are a named list (`RefusalBoundaryGateTests.Space_is_read_only_where_named`).
  - Data Protection keys are persisted (`Yello.Host/Deployment/PersistedKeyRing.cs`) — relevant only to Q1 (b).
- **Host:** `ApiRouteGroup` filter order (RequireAccount → AccountScopedResolution → Antiforgery → NotAvailableFloor → Resolve → CreateSpace); `ActiveSpaceResolution.Creation.cs` (the non-`{spaceId}` unit-of-work step to model); `ActiveSpaceResolution.Bound.cs` (`CreationBound.ConsultAsync`); `RequiredBound`/`ConsultsBound`; `BoundaryProblems.BoundReached`; `NotAvailableFloor`; `RegisterAccountEndpoint` (rate-limited `CredentialHashingRateLimit`); `Microsoft.AspNetCore` logs at Warning and the EF command log at Warning (`Program.cs`) — keep both.
- **Client:** `Register.razor` (three fields; completion copy "Your Account is ready, and so is your Space." — it does not sign in), `SignIn.razor` (lands on `Home`, no return URL), `ClientRoutes` (`IsReachedWithoutASession` covers sign-in and register only), `SpaceNavigation.GoTo`, `SpaceDirectory`, `RefusalCopy`, `RoleCopy`, `LiveAnnouncement`, `staticwebapp.config.json` (navigation fallback only, no `routes`/headers), `index.html` (`<title>Yello</title>`, no `og:`).
- **Tests:** `InvitationWire`, `MailDrop`, `WireDatabase` (`AddInvitationAsync`, `SetInvitationStateAsync`, `InvitationsInAsync`, `OutboxRowsAsync`), `SpaceSeeding.SeedMembershipsAsync`, `DurationIndistinguishability`, `IsolationTimingTests`, `IsolationCases.Invitations.cs`, `FakeYelloApi.Invitations.cs`, the Slices partials `ActiveSpaceEndpointTests.*Invitation*.cs`, the gates in `RefusalBoundaryGateTests.Invitations.cs` / `.InvitationRevoke.cs`, `SpaceSwitcherGateTests(.Creation).cs`, `SpaceResolutionSeamTests`, `SchemaVarianceTests`, `IsolationCaseListTests`.

### What must be preserved (regression guard)

- **Registration byte for byte** (AD-22, AD-23): `RegisterAccountHandler`, `AccountRegistrationStore`, `RegisterAccountEndpoint`, `RegistrationDurationTests` and its permanent plant, the EF command log filter. Under Q3 (a) none of these files changes.
- **4.1's issue and 4.2's list and revoke**, including the revoke race case, the issue race case, AD-23's issue oracle and its timing row.
- **No second both-keys unit of work**; one key-setting line; no `HOLDLOCK`; no timer or hosted service; Space deletion stays one statement (its cascade must still take the new token index rows, if Q1 (a)).
- **The boundary `404` stays byte-identical** (`RefusalCopyTests.Every_not_found_cause_renders_the_same_refusal`). The new `invitation-not-valid` body is a different type for a different route; it must not change `not-available`.
- **The Space settings narrated exit**, `SpaceNavigationGateTests`, `ClientStaticStateGateTests`, `ClientStorageBanTests`.
- **`IdempotencyStore` refuses to save with other changes pending:** the accept's `ExecuteUpdate` tracks nothing, but the Membership `Add` + `SaveChanges` must complete before the record saves (the store saves its own effect, as every store does).

### Locking and the races (RCSI)

- The accept takes `Yello/Invitations/{spaceId}` first (Q8), then reads the caller's address (policy-free `Account`), counts Memberships (pipeline, Q9), runs the guarded `UPDATE`, inserts the Membership. Issue takes the same lock first, so an issue and an accept for the same address serialise (closes ledger `:725`). The revoke takes no lock; its guarded `UPDATE` waits on the accept's uncommitted row and re-evaluates (4.2's analysis holds from both sides).
- Lock order is the application lock, then row locks, everywhere. No `HOLDLOCK` (3.4).
- The deterministic race recipes are in AC5 and AC9.

### Open questions

**[LEE]** marks a question whose answer touches isolation, RLS, a `SESSION_CONTEXT` key, a pre-Space-context or cross-Space read, or security. Q1-Q7 go to Lee before development; each has concrete options and a recommendation. Q8-Q19 are decided by the dev phase as reversible, recorded decisions; take the recommendation unless the code shows it is wrong, and record why.

**Decided by Lee, 2026-11-01 (not open):** Q1 - (a) a policy-free `InvitationToken` index table, as a named AD-2 variance beside `ApiToken.BoundSpaceId`; Q2 - (a) one named unit of work that sets the `SpaceId` key alone, from the server-resolved Space id, with a spine amendment and the named-setter gate (not combined with (c)); Q3 - (a) client-chained register → sign in → accept, registration unchanged; Q4 - (a) token in the `Yello-Invitation-Token` header for the view and in the body for the accept, never in a URL path; Q5 - (a) mismatch detected at view time when a Session exists, and always at accept; Q6 - (a) one byte-identical `404 invitation-not-valid` for the five states on both routes, same work plus a duration floor, nothing recorded; Q7 - (a) the accept is Session-only, a bearer Token gets the byte-identical `401`. Q8-Q19 remain for the dev phase.

1. **[LEE] Resolving the token before any Space context exists** (ledger `:718`, C9). `Invitation` is under the Space policy; the request has only the token.
   - **(a) Recommended. A policy-free `InvitationToken` index table** — `TokenHash` (unique), `InvitationId`, `OfferedSpaceId` — written in the issue's transaction, cascading from `Invitation (SpaceId, Id)`, read by exactly one named type, and named in `SchemaVarianceTests` as a variance beside `ApiToken.BoundSpaceId` ("looked up by hash before any Space is known"). It discloses only hash → Space id, to a caller who already holds the 256-bit token. Cost: a second policy-free Space-referencing table, a migration with a backfill, a named-reader gate, and a recorded AD-2 variance.
   - (b) **A Data-Protection-sealed token** carrying `SpaceId` (and `InvitationId`) under authenticated encryption, unsealed with the persisted key ring; then the lookup by `TokenHash` runs under RLS. No new table. Cost: changes 4.1's token shape and its remarks ("nothing encoded"); tokens grow to ~150 characters; a key-ring loss or revocation silently turns every outstanding link into "no longer valid"; the Space key is then set from a value the client carried (sealed, but client-supplied in AD-2's words).
   - (c) A SQL module or predicate exemption that lets one lookup read `Invitation` without a key (signed procedure, `EXECUTE AS`, a third session key, or a predicate clause on token hash). **Relaxes RLS — the bypass AD-24 exists to forbid. Never build it.**
2. **[LEE] Setting the `SpaceId` key from the token-resolved Space, for a caller who is not a Member** (AD-2, AD-24, AD-28; ledger `:213`, `:670`). Both the anonymous view and the authenticated accept need a unit of work keyed to the invited Space with no `ActiveSpaceContext` (the caller holds no Membership there yet), and the view reads the policy-free `Space` row's name for a non-member.
   - **(a) Recommended. One named unit of work (e.g. `InvitationScope`) that sets the `SpaceId` key alone**, from the server-stored Space id the token resolved to (never from the request body or URL), narrows `SpaceScope` without resolving a Role, and is the only way the view and the accept touch the Space. The view reads only the Invitation's offer fields through `Actionable` and the Space's name; the accept writes only the Invitation's state and one Membership. Recorded as a dated spine amendment (AD-2's key-source rule and AD-28), with gates: the named list of `SpaceId`-key setters, the unit's dependency allow-list, `Space_is_read_only_where_named` extended by one reader. It sets no `AccountId` key, so it is **not** a third Account-scoped surface and AD-24's rule line is untouched. Cost: a fourth source for the `SpaceId` key (after the resolver, registration and Space creation) and an amendment.
   - (b) A both-keys unit of work in `SpaceCreationScope`'s style. Rejected: it widens `Membership`'s filter to the caller's other Spaces for no need, and it still cannot write into two Spaces (C1).
   - (c) Capture the Space name on the Invitation at issue (the `IssuerName` precedent) so the view reads no `Space` row. Combinable with (a); reduces the `Space` read but shows a pre-rename name after a rename. Cost: a column and a stale-name rule.
3. **[LEE] The shape of registration-while-accepting** (AC3; FR-11 `:278-279`; AD-22, AD-23; ledger `:213`, `:277`).
   - **(a) Recommended. Client-chained: register (unchanged route and slice) → sign in → the AC4 accept.** One deliberate act by the person. AD-22 and AD-23 hold by construction because nothing in registration changes. The address binding is the same as for an existing Account: the accept succeeds only if the new Account's normalised address equals the Invitation's. Cost: the AC's "that registration **is** their act of acceptance ... creates exactly one additional Membership" is met by the person's single act, not by the registration request (C2); a refused accept leaves a registered Account with its Personal Space and no invited Membership (a valid state, retryable while the Invitation is actionable).
   - (b) Server-side: `RegisterAccountRequest` gains an optional invitation token; after `RegisterAccountHandler` commits, a second unit of work (Q2's) accepts. Cost: registration's response must stay uniform for a known address, so the acceptance step must run identical statements whether or not the Account was created (AD-23 duration), and the client still has to sign in and find the Space; registration's own files change.
   - (c) One transaction for both Spaces. **Impossible without relaxing `Membership`'s BLOCK predicate** (ledger `:213-219`, C1). Never build it.
   - Security note for Lee, either option: registration has no email verification, so the binding rests on token possession plus knowing (and typing) the invited address. The view never shows it. A forwarded-link holder who also knows the address can register it and join. That is the same trust direct registration already extends to an unverified address, now with a Space at stake.
4. **[LEE] Where the token travels on the API** (NFR-6, I-17).
   - **(a) Recommended.** View: `GET /api/v1/invitations/offer` with the token in a request header (`Yello-Invitation-Token`). Accept: `POST /api/v1/invitations/acceptance` with `{ "invitationToken": "..." }` in the body, plus `Idempotency-Key` and the anti-forgery token. The token then appears in no API request line, so no proxy, ingress or access log records it. Cost: a custom header; CORS must allow it (`CrossOriginPolicy`).
   - (b) The token in the path: `GET /api/v1/invitations/{token}` and `POST /api/v1/invitations/{token}/acceptance`. RESTful and simple. Cost: the token sits in every request line any layer logs (Container Apps ingress included); `Microsoft.AspNetCore` is at Warning today, but that is a configuration, not a guarantee.
5. **[LEE] When the wrong-Account mismatch is detected** (AC7).
   - **(a) Recommended. At view time when a Session is present** (`AddressedToYou: true|false`, `null` without a Session), and always at accept. Only for an actionable Invitation; a non-actionable one answers AC6 for everyone. Disclosure: a signed-in link holder learns that their own address is not the invited one, which the AC requires; neither address is returned.
   - (b) At accept only: the view always shows the offer, and "Join" answers the `403`. Cost: the person clicks before learning; the AC says "when the link is opened".
6. **[LEE] The uniform response and its duration** (AC6; UX-DR18; AD-23's discipline).
   - **(a) Recommended.** One `404` with `urn:yello:problem:invitation-not-valid`, byte-identical for the five states, on both routes. Equal duration by doing the same work (an unrecognised token sets the key to a server-generated sentinel id and runs the same read, which sees nothing) **and** a floor in the `NotAvailableFloor` style on both routes. Proved with `DurationIndistinguishability` and a permanent planted oracle. Nothing recorded as an `AccessRefusal`.
   - (b) `410 Gone` for the same body. Cost: "gone" is false for a token that never existed, and any status that differs by state is the oracle.
   - (c) `200` with a `state` field. Rejected: it names the state.
7. **[LEE] Credentials on the two routes** (AD-4 parity; the isolation suite's two surfaces).
   - **(a) Recommended.** The accept is **Session-only**: a bearer Token gets the byte-identical `401`, as Space creation does ("a Token is no credential here: its scheme authenticates nothing on a route without `{spaceId}`"). A Token is bound to one Space and must never join its Account to another. The view ignores credentials for the offer (same answer with none, a Session or a Token), except that Q5's indicator reads a Session only.
   - (b) Accept with a Token for API parity (FR-35). Rejected: a script holding a Space-bound Token could join its Account to other Spaces.
8. **The Invitations lock on acceptance** (ledger `:725`). **(a) Recommended:** the accept takes `Yello/Invitations/{spaceId}` first; the same lock serialises the Memberships count (acceptance is the only creator of Memberships in an existing Space). Cost: accepts and issues in one Space queue at one lock. (b) A separate `Yello/Memberships/{spaceId}` lock with a fixed order. Cost: two locks, an order rule.
9. **Order of checks inside the accept.** **(a) Recommended:** lock → actionable and addressed to the caller → bound → write. A non-addressee never learns the Space is full. The pipeline still owns the count (AR-3): the step calls `CreationBound.ConsultAsync` after the store's guard read and before its write, or the store's guarded read is exposed to the step as a port. (b) Bound first (the existing pipeline order). Cost: a full Space is disclosed to a revoked-token holder.
10. **The success answer.** **(a) Recommended:** `200` `AcceptedInvitationResponse(Guid SpaceId)`. (b) `204` and the client re-reads the switcher to find the Space by name. Cost: names are not unique.
11. **Idempotency on the accept** (AD-18). **(a) Recommended:** honour `Idempotency-Key`, recorded in the invited Space's unit of work (the Space-scoped `IdempotencyStore`, keyed with the caller's Account), so a retry after a lost `200` replays it instead of answering "no longer valid". Verify the store works with a `SpaceScope` narrowed but not resolved; if it does not, use the Account-scoped record (`AccountIdempotencyRecord`) and record why. (b) No key; the guard alone. Cost: a lost response looks like a revoked Invitation.
12. **The caller is already a Member of the Space at accept** (should be unreachable: issue refuses a Member, and one actionable Invitation per address). **(a) Recommended:** `409 already-a-member` (4.1's type), Invitation untouched, nothing written — the caller can see their own Membership. (b) AC6's uniform answer.
13. **The mismatch status.** **(a) Recommended:** `403 invitation-for-another-account`, unrecorded. (b) AC6's uniform `404`. Cost: the AC requires the mismatch to be *stated*.
14. **`MembershipChanged` on acceptance** (AD-9). **(a) Recommended:** not published — no publisher exists, and the new Member has no live session in the Space to update. Ledger to **7.7** (with 7.3): "acceptance publishes `MembershipChanged(AccountId, SpaceId)` at its transaction boundary once the publisher exists". (b) Build a publisher seam now. Scope creep.
15. **Copy** (`mockups/invitation.html`; `EXPERIENCE.md:99`; sentence case, no exclamation marks). **(a) Recommended:** "{Issuer} invited you to {Space}." / "Join as {Role}." / "Create an Account and join" / "Already have an Account? Sign in" / "Join {Space}" / "Creating your Account." / "Joining." / "You'll also get a Space of your own." / "You joined {Space}." / "This invitation is no longer valid." + "Ask whoever invited you to send a new one." + "Go to Yello" / "This invitation is for a different Account." + "Sign out and sign in as the Account it was sent to." + "Sign out" + "Cancel" / "Loading the invitation." (visually hidden) / "Not sent.". (b) Any equivalent keeping the rules.
16. **Sign-in from the view.** **(a) Recommended:** inline on the view (reuse the sign-in fields and `SessionState`), no return-URL parameter, so there is no redirect surface and the token stays in the page URL only. (b) `/sign-in?return=/invitations/{token}` with a same-origin allow-list. Cost: a redirect parameter to validate, and the token in a second URL.
17. **Where an expired Invitation's "lapsed" sits.** **(a) Recommended:** no stored form (4.1 Q11); "lapsed" in UX-DR18 is response vocabulary only and is covered by the derived-expired arm. Record it.
18. **The IL gates' reach** (ledger `:743`). **(a) Recommended:** add a source-text scan for raw SQL naming `Invitation` outside the named store(s), and state in the gates' remarks that an expression-tree filter inside the store is not seen. (b) State the reach only.
19. **The landing after accept.** **(a) Recommended:** `SpaceNavigation.GoTo(ClientRoutes.SpaceHome(spaceId))` after `SpaceDirectory.Forget`, so the switcher lists the new Space. (b) Land on Home and let the entry point pick. Cost: Home picks the first listed Space, not the invited one.

**Summary.** Q1-Q7 are [LEE]. Q1 (a) adds a policy-free table under a named variance; Q2 (a) adds a fourth `SpaceId`-key source as a named unit of work and a spine amendment; Q3 (a) keeps registration unchanged and makes acceptance a separate request; Q4-Q7 (a) relax nothing. No recommended option disables a policy, adds a session key, adds a both-keys unit of work, or relaxes a BLOCK predicate.

### Contradictions found (record, do not resolve beyond what is stated)

- **C1. AD-24's 2026-10-29 amendment assumes 4.3 needs both keys.** It does not help: `Membership`'s BLOCK AFTER INSERT predicate is Space-only, so no single unit of work can insert Memberships into the Personal Space and the invited Space. Whatever Lee decides, registration-while-accepting is two units of work.
- **C2. AC3's "that registration is their act of acceptance ... in the same transaction ... creates exactly one additional Membership"** against C1. AD-28 says "AD-22 provisions their own Personal Space in the same transaction independently of the Space they were invited to" — "same transaction" binds Account and Personal Space; the invited Membership is "separate, additional" (AD-22). Read AC3 accordingly; Q3 decides the mechanism.
- **C3. The terminal-state vocabulary** (FR-10 "expired"; AD-28 "lapsed"; UX-DR18 five words; AD-27 never stores expired). Settled by 4.1 Q11; Q17 restates it.
- **C4. Duration uniformity** is required by UX-DR18 and `EXPERIENCE.md:264`, not by AD-23's text (registration, authentication, issue) or §6.1. Built anyway (AC6).
- **C5. AD-26's `409` for a no-longer-pending transition** against the epics' uniform response for "accepted". On this route the AC wins: the caller is usually not a Member, and the 409 would make the route an oracle.
- **C6. §7 and `EXPERIENCE.md:47` list "accept an Invitation" as unauthenticated**; FR-11 `:277` and AD-28 require authentication to accept. Only the view is unauthenticated.
- **C7. AD-3: "every Space-scoped route carries `{spaceId}`"**, while the token routes cannot (the client does not know the Space before the view, and the page URL must not carry it). The routes are not resolved by the Membership pipeline; they still need isolation rows (AC10).
- **C8. The folder.** `ARCHITECTURE-SPINE.md:274`/`:395` show `Memberships/...`; 4.1 C4 and 4.2 C2 settled `Application/Invitations/`. Use `Application/Invitations/ViewInvitation/` and `Application/Invitations/AcceptInvitation/`.
- **C9. AD-9's `MembershipChanged`** on any Membership mutation, against no publisher before 7.7 (Q14).
- **C10. The mockup's arrival heading** ("Ravi Anand invited you to a Space.") does not name the Space; the AC and `DESIGN.md:480-482` ("this spine wins on conflict with any mock") require it. Q15 names it.

### Ledger dispositions (recommended; the dev phase writes them in `deferred-work.md`)

- `:718` (C9) → **CLOSED by 4.3 (Lee's Q1 (x))**, naming the mechanism, the variance entry and the gate.
- `:213` → **CLOSED by 4.3 (Lee's Q3 (x))**: registration and acceptance are two units of work; state the partial-failure behaviour and the C1 correction to AD-24.
- `:277` → **CLOSED by 4.3**: registration gains no filtered read (Q3 (a)), or the acceptance unit narrows `SpaceScope`.
- `:670` → **UPDATED by 4.3**: the acceptance unit's allow-list gate exists; the creation unit's own allow-list stays open unless built in the same pass (owner unchanged otherwise: give it one).
- `:725` → **CLOSED by 4.3 (Q8)** with the race case name.
- `:736` → **CLOSED by 4.3** with the five-state case name.
- `:722` → **UPDATED by 4.3**: the Space-deleted arm answers the uniform response (case name). Owner stays Lee for the delivery itself.
- `:723` → **UPDATED by 4.3**: the accept is the third sanctioned write. 5.3 keeps the `OwnershipOffer` half.
- `:743` → **CLOSED or UPDATED by 4.3 (Q18)**.
- `:36` → **UPDATED by 4.3**: the invitation view's 6px clause; still unmapped: buttons, the Offer indicator, dialogs other than the panel.
- `:20` → **UPDATED by 4.3**: the invitation view under both adaptations. It stays open.
- **New entries:** `MembershipChanged` on acceptance (owner **7.7**, low; Q14); if Q6's planted oracle is not caught, the masked duration proof (owner **Lee/TEA**, medium); if Q3 (a), "an accept refused after registration leaves an Account with no invited Membership" as a recorded behaviour (owner **UX**, low) — or none if UX already accepts the copy.

### Architecture compliance (must)

- **AD-21 ring.** Handlers in Application, stores and the unit of work in Infrastructure, the pipeline step and endpoints in Host, contracts in Contracts. Check `AllowedReferenceEdges.cs`.
- **AR-3.** Slices never read Role, count a bound, set a key or record idempotency.
- **AD-2.** No `Where(SpaceId == ...)` needed under the unit; adding one is redundant but allowed. Raw SQL only in Infrastructure (`No_production_code_outside_Infrastructure_bypasses_the_query_filters`).
- **AD-27.** One predicate, composed, translated, database clock. No C# expiry check.
- **AD-23.** No existence branch in registration; no address in any response of these routes.
- **Logs.** Event ids and `InvitationId`/`SpaceId` only after resolution; never the token, the address, the Space name or the issuer name. `No_log_message_takes_an_address_a_token_or_an_email_body` stays green.
- **No test-only code in production** (1.9 Q4).

### Library / framework

No new package. EF Core 10.0.11 (`ExecuteUpdateAsync`, `SqlQuery`, hand-edited migration), ASP.NET Core 10.0.11 minimal APIs, Blazor WebAssembly 10.0.11 (`PageTitle`, `HeadContent`), Microsoft.Data.SqlClient 7.0.2 (`sp_getapplock` through `ApplicationLocks`), xunit.v3 4.0.0, Testcontainers 4.6.0, Playwright 1.62.0, ArchUnitNET 0.13.3, Mono.Cecil (the IL gates). Azure Static Web Apps `staticwebapp.config.json` supports per-route `headers` under `routes` (route-level) and `globalHeaders`; verify the exact schema against Microsoft's current SWA configuration documentation before writing it. No live web check was run in this phase.

### Testing requirements

Follow `tests/TESTING-CONVENTIONS.md`:

- Traits: `Suite`, `Priority`, `Requirement` (FR-11, AR-30/AD-25, AR-32/AD-28, AR-27/AD-22, AR-31/AD-27, UX-DR18, UX-DR35, NFR-8), `Assumption` (`PRD-12-9` for anything resting on the 7 days) and `Tier`. Underscore-sentence names. Never `Task.Delay`; races wait on `LCK_M_%`.
- **Never a shared email address** (`InvitationWire.FreshAddress`). Mail assertions scoped to the case's own address.
- Seed through the product; `WireDatabase` only for the expired row and seeded Memberships for the bound.
- **No new container-backed test class.** Slices partials `ActiveSpaceEndpointTests.ViewInvitation.cs`, `.AcceptInvitation.cs`, `.AcceptInvitationRace.cs`, `.AcceptInvitationBound.cs`; rows in `IsolationCases.Invitations.cs`; the timing case in the existing timing collection (`IsolationTimingTests` or its Slices sibling). Handler unit tests take no database.
- **Every absence has an anchor and is seen red under a plant.** A plant caught 0/3 is recorded as masked, never deleted.
- **Memory budget (CLAUDE.md).** One `dotnet test` at a time; iterate with `--project ... --filter`; full run with `--max-parallel-test-modules 1`; rerun Client failures alone.

**Slices (wire), each its own test:** the view of a live Invitation answers exactly the offer fields; the view writes nothing (AC1); the five-state byte equality on both routes (AC6); a matching Account accepts → `200`, one Membership at the Invitation's Role, `Accepted`; a second accept → uniform; idempotent replay → original `200` (AC11); register → sign in → accept (AC3); the mismatch on view and accept with no address (AC7); the existing-Account world (AC8); the bound at 100 and at 99 (AC9); the three races (AC5) and the bound race (AC9); a `GET` to the accept route writes nothing; no Token on accept (`401`); I-17: the token and the addresses never reach the Host log.

**Architecture gates:** AC12's list; the contract-shape gates for the view response and the accept request; the problem types declared with client copy naming 4.3 (sibling gate, as 4.2 did); `index.html` has a fixed title and no `og:`/`twitter:`; `staticwebapp.config.json` carries `X-Robots-Tag: noindex` and `Referrer-Policy: no-referrer` for `/invitations/*`; the client route segment equals `InvitationLinks.Path`; `MapIssueInvitation` declares no bound; the design gates of AC2.

**Isolation:** the AC10 rows. **Client:** the AC13 list.

**Minimum plant table** (restore each in a `finally`; rebuild `Yello.Client` in Debug before each browser plant):

| # | Plant | Expected red |
|---|---|---|
| 1 | The view sets `State = Accepted` | AC1 safe-read case |
| 2 | The accept without the `Actionable` guard | AC5 race 2; AC6 revoked and expired arms |
| 3 | The accept as load, C# check, `SaveChanges` | AC5 race 1 or 2 (or masked) |
| 4 | The accept without the address clause (token as authority) | AC7 accept case; `ISO-ACCEPT-INVITATION-OTHER-ACCOUNT` |
| 5 | The accept without the Invitations lock | AC5 race 3 (or masked) |
| 6 | The bound count skipped, or the bound checked before the guard | AC9 at 100; the non-addressee-full-Space case |
| 7 | An unrecognised token answers a distinct body or returns early | AC6 byte case; the permanent duration oracle |
| 8 | The view body gains `SpaceId` or the address | the response-shape gate; `ISO-INVITATION-VIEW-DISCLOSES-ONLY-THE-OFFER` |
| 9 | The mismatch copy names the invited address | AC7 client case |
| 10 | The client sends the accept on page load | AC4 client "no accept on navigation" case |
| 11 | A Token accepted on the accept route | `ISO-ACCEPT-INVITATION-NO-SESSION` Token arm |
| 12 | A second `SpaceId`-key setter type | `The_SpaceId_key_is_set_by_exactly_the_named_units_of_work` |
| 13 | The accept's `State` write moved to another type | `Only_the_Invitation_store_writes_an_Invitation_state` |
| 14 | The view's own `ExpiresAt >` comparison | `Only_the_shared_actionable_predicate_compares_ExpiresAt` |
| 15 | The noindex header removed from `staticwebapp.config.json` | the config gate |
| 16 | The token logged | the I-17 row log check |
| 17 | The acceptance handler references `NewSpace` (a second provisioning path) | the AD-22 gate |

**Run** (CLAUDE.md): `docker info --format '{{.ServerVersion}}'`; `dotnet build Yello.slnx` (0/0); `dotnet build-server shutdown`; `dotnet test Yello.slnx --no-build --max-parallel-test-modules 1`.

### Previous story intelligence (4.2, 4.1)

- **4.2:** one guarded `ExecuteUpdateAsync` composed with `OfferPredicates.Actionable` is a single `UPDATE` that waits and re-evaluates under RCSI (its race case was green first time and red under the unguarded and load-check-save plants). The accept uses the same shape. The revoke's existence read decides `409`/`404`; the accept's disambiguation read decides mismatch/uniform and must not leak more.
- **4.2 review:** stale overlapping client reads overwrote newer ones (use a generation counter); focus must never fall to `body` after an answer (UX-DR39); the fake API must answer what the Host answers and use contract constants.
- **4.2 P9 lesson:** a wire case may not catch a plant the gate catches; record which test is the real guard.
- **4.1 review P10:** no `_ =>` arm mapping an unknown outcome to `404`. **P13:** no literals in the fake. **P14:** never poll SQL back to back.
- **4.1 D4 (Lee/TEA):** the issue's planted duration oracle is a proxy; do better here or record masking honestly.
- **S1200** has pushed stores and pipeline steps into nested or split types; expect it in the pipeline step and `InvitationStore`.
- **Every Host sweeps the whole outbox at start**: assert per address.
- **Lee's earlier decisions stand:** 4.1 Q1 cascade, Q2 Admin offers Admin, Q3 NFC + BIN2, Q4 no package, Q6 policy-free outbox; 3.1 Q1 (a) the one both-keys unit; sign-out is server-side via `SessionsValidAfter`.

### Git intelligence

Recent commits: `99bd3d3` (4.2 review close), `71e68ac` (4.2 implementation), `53b087c` (4.2 ATDD scaffolds), `f908505` (4.2 story), `4a393a4` (4.1 review close). The pattern is story → (Lee's [LEE] decisions recorded in the story) → ATDD scaffolds (`[Fact(Skip = "red: ACn - ...")]`, `atdd-checklist-<key>.md`, rows with `PendingUntil`) → implementation → review fixes. The orchestrator commits each. 4.2's File List (`4-2-see-and-revoke-pending-invitations.md:730-768`) names the files this story extends.

### Project Structure Notes

- **New (expected):**
  - `Yello.Application/Invitations/ViewInvitation/ViewInvitationHandler.cs`, `Yello.Application/Invitations/AcceptInvitation/AcceptInvitationHandler.cs`, their read models/outcomes
  - `Yello.Contracts/Invitations/InvitationOfferResponse.cs`, `AcceptInvitationRequest.cs`, `AcceptedInvitationResponse.cs`
  - `Yello.Infrastructure/Persistence/InvitationScope.cs` (name per Q2) and, under Q1 (a), the `InvitationToken` entity, configuration and migration
  - `Yello.Host/Spaces/ActiveSpaceResolution.Invitation.cs` (or a sibling static class), `Yello.Host/Endpoints/InvitationEndpoints.cs` additions
  - `Yello.Client/Pages/InvitationView.razor`
  - tests: the Slices partials above, handler unit tests under `tests/Yello.Tests.Slices/Invitations/AcceptInvitation/`, gate additions in `RefusalBoundaryGateTests.Invitations*.cs`, `SpaceSwitcherGateTests` / `SpaceResolutionSeamTests` (setter list), `DesignFoundationGateTests` (invitation view), client `InvitationViewTests.cs`
- **Updated (expected):** `InvitationRoutes.cs`, `ProblemTypes.cs`, `IInvitationStore.cs`, `InvitationStore.cs`, `BoundCounter.cs`, `Program.cs`, `ClientRoutes.cs`, `staticwebapp.config.json`, `ClientCopy.resx`, `RefusalCopy.cs`, `components.css`, `Invitation.cs`/`InvitationState.cs`/`OfferPredicates.cs`/`InvitationTokenSecret.cs`/`InvitationLinks.cs`/`AccountRegistrationStore.cs`/`RegisterAccountHandler.cs` (comments only), `SchemaVarianceTests.cs` (Q1 (a)), `IsolationCases.Invitations.cs`, `IsolationCaseListTests.cs` (only if a route is legitimately credential-only — none is expected), `FakeYelloApi.Invitations.cs`, `TextSpacingTests.cs`, `ARCHITECTURE-SPINE.md` (amendment), `deferred-work.md`, `sprint-status.yaml`.
- **No new project, no new package, no new container-backed test class.**

### References

- `epics.md`: `:96`-`:98` (FR-11 inventory), `:213-221` (AR-25 to AR-33), `:256` (UX-DR18), `:271`, `:275`, `:278`, `:370`, `:1613-1617` (epic 4), `:1703-1753` (4.3), `:1755-1835` (4.4, 4.5 boundaries).
- `prd.md`: `:53-63` (glossary), `:104-107` (UJ-2, UJ-3), `:134-143` (FR-1), `:271-291` (FR-11, FR-12), `:643-652` (FR-39), `:743-749` (NFR-8), `:755` (NFR-9), `:763` (§6.1), `:802` (§7), `:862` (SM-4), `:900` (§12 #9).
- `ARCHITECTURE-SPINE.md`: `:77-86` (AD-2), `:88-94` (AD-3), `:130` (AD-9), `:182-187` (AD-18), `:207-255` (AD-22 to AD-28), `:274`, `:395`, `:418`.
- `DESIGN.md`: `:270-276`, `:439`, `:475`, `:480-482`. `EXPERIENCE.md`: `:47`, `:69`, `:99`, `:177-178`, `:219`, `:264-265`, `:273`, `:276`, `:489-499`. `mockups/invitation.html`.
- `specs/spec-yello/`: `SPEC.md:77-79`, `:201`; `acceptance-criteria.md:116-124` (CAP-11); `quality-budgets.md:72`.
- `test-design-qa.md`: `:129-133`, `:165` (R2), `:271` (I-7), `:277` (I-13), `:278` (I-14), `:305` (A-7), `:308` (A-10), `:313` (P-5), `:314` (P-10). `test-design-architecture.md`: `:212`, `:237` (R12), `:319-327`, `:424-439`.
- `implementation-readiness-report-2026-08-22.md`: `:603-631`, `:1048`, `:1372-1376`, `:1416`. `sprint-change-proposal-2026-09-13.md:90`.
- `tests/TESTING-CONVENTIONS.md`: `:44-85`, `:87-200`, `:201-412`, `:414-456`, `:458-490`, `:492-518`, `:519-540`, `:704-811`.
- `deferred-work.md`: `:20`, `:36`, `:213-221`, `:277`, `:670`, `:718`, `:722`, `:723`, `:725`, `:736`, `:743`.
- `4-1-invite-an-email-address-to-a-space-at-a-role.md`: AC3, AC5, AC7, AC8, Q11, C2, C9, Review Findings (D4, P10, P13, P14). `4-2-see-and-revoke-pending-invitations.md`: AC2, AC4, Q2, Q3, Q10, Q11, "Locking and the race", Review Findings, Debug Log.

### ATDD artifacts - red-phase scaffolds already in the tree

**Checklist:** `_bmad-output/test-artifacts/atdd-checklist-4-3-accept-an-invitation.md` (story 4.3 ATDD run, 2026-11-01;
scaffolded against Lee's Q1-Q7 (a) and the Q8-Q19 recommendations, each named in its skip reason).

**Scaffold files** (every case `[Fact(Skip = "red: AC<n> - ...")]` / `[Theory(Skip = ...)]`, every isolation row
`PendingUntil: "4.3"`):

| File | Cases | ACs |
|---|---|---|
| `tests/Yello.Tests.Slices/Spaces/ResolveActiveSpace/ActiveSpaceEndpointTests.ViewInvitation.cs` | 4 | AC1, AC2, AC7 |
| `tests/Yello.Tests.Slices/Spaces/ResolveActiveSpace/ActiveSpaceEndpointTests.AcceptInvitation.cs` | 10 | AC3, AC4, AC5, AC6, AC7, AC11, Q12, I-17 |
| `tests/Yello.Tests.Slices/Spaces/ResolveActiveSpace/ActiveSpaceEndpointTests.AcceptInvitationRace.cs` | 3 | AC5 (races 1-3) |
| `tests/Yello.Tests.Slices/Spaces/ResolveActiveSpace/ActiveSpaceEndpointTests.AcceptInvitationBound.cs` | 4 | AC9 |
| `tests/Yello.Tests.Isolation/IsolationCases.InvitationAcceptance.cs` (hooked into `IsolationCases.Invitations.cs`) | 9 rows (18 when active) | AC6, AC7, AC8, AC10 |
| `tests/Yello.Tests.Isolation/IsolationTimingTests.cs` (permanent duration oracle added) | 1 theory (2) | AC6 |
| `tests/Yello.Tests.Architecture/RefusalBoundaryGateTests.InvitationAcceptance.cs` | 12 (8 skipped, 4 active) | AC3, AC4, AC5, AC6, AC7, AC9, AC12 |
| `tests/Yello.Tests.Architecture/SpaceSwitcherGateTests.Invitation.cs` | 2 | AC12 (setter gate, spine amendment) |
| `tests/Yello.Tests.Architecture/DesignFoundationGateTests.InvitationView.cs` | 7 (5 skipped, 2 active) | AC2, AC13 |
| `tests/Yello.Tests.Client/InvitationViewTests.cs` | 10 | AC13 (with AC2, AC3, AC4, AC6, AC7, AC9) |
| `tests/Yello.Tests.Client/TextSpacingTests.cs` (one theory added) | 1 theory (2) | AC13, ledger `:20` |

Harness (no product code): `InvitationWire` (five TEMPORARY stand-ins - `OfferRoute`, `AcceptanceRoute`,
`TokenHeaderName`, `NotValidProblemType`, `ForAnotherAccountProblemType` - to swap for the Task 3 contract members and
delete; plus `TokenIn(email)`, `AcceptBody`, `UnrecognisedToken`, `MalformedTokens`, `OfferedInvitation`),
`WireDatabase.SetInvitationExpiresAtAsync` / `MembershipsOfAccountAsync` / `AccountCountAsync`, `IsolationWorld.Mail`,
`CaseRequest.Headers`, `ProductActor.SendAsync` with a nullable body, `FakeYelloApi.InvitationAcceptance.cs` (and the token
header allowed in the fake's CORS).

**Green today, left active (standing regression gates, each with a detector oracle):**
`No_raw_SQL_names_an_Invitation_table_outside_the_Invitation_store` (+ `The_raw_SQL_scan_detects_a_plant`),
`Only_the_registration_slice_constructs_an_Account` (+ `The_Account_construction_scan_detects_a_plant`),
`The_host_page_has_a_fixed_title_and_no_preview_metadata` (+ `The_preview_metadata_scan_detects_a_plant`). Every other new
case is skipped. Red verified by temporary unskip (restored in a `finally`): the view case and race 3 both failed with the
unmapped route's `404 not-available`, the stated reason. Full run after this ATDD phase (`dotnet build Yello.slnx` 0
warnings, 0 errors; `--max-parallel-test-modules 1`): 2,029 total, 1,971 passed, 0 failed, 58 skipped (baseline 1,975 /
1,965 / 0 / 10, plus 48 skipped scaffolds and 6 active gates).

**Activation order (riskiest assumption first):**

1. `Five_states_are_one_answer_on_the_view_and_on_the_accept` - token resolution before any Space context through the
   policy-free index (Q1), the `SpaceId`-only unit reading through RLS and `Actionable` (Q2), the sentinel key for an
   unrecognised token, the index's cascade with its Space, and one byte-identical body (Q6); then the two isolation
   five-state rows.
2. The view: `The_view_answers_exactly_...`, `Viewing_an_Invitation_writes_nothing_...`, the credential and mismatch cases,
   the view contract gate, `ISO-INVITATION-VIEW-DISCLOSES-ONLY-THE-OFFER`.
3. The setter, token-index and dependents gates; the spine amendment (Task 2) and its gate.
4. The accept: joins, race 2, race 1, race 3, second acceptance, mismatch, no-Session/Token, GET, already-a-Member, replay,
   the third-write and route gates.
5. The bound: at 100, at 99, the bound race, the full-Space ordering case.
6. Registration from the Invitation and the AD-22 slice gate; the I-17 log case; the remaining isolation rows - clear every
   `PendingUntil: "4.3"` before review (GATE-003).
7. The timing row and its permanent oracle (record masking honestly if the oracle is not caught).
8. Handler unit tests (dev-written), the problem-type gate, the client gates.
9. The browser cases, after `Yello.Client` is built in Debug.

Findings for dev (detail in the checklist): race 3's end state cannot show a missing lock (the issue's own
`invitation-pending` check refuses), so its decisive assertion is that the accept waited; `Space_is_read_only_where_named`,
`SchemaVarianceTests.NamedVariances` and the Host's `CrossOriginPolicy` (the token header) are active gates/config the dev
phase amends, not scaffolded.

Activate one at a time, confirm it fails for the stated reason, then implement. A scaffold that is wrong is corrected and
the correction recorded in the Debug Log, not deleted.

## Dev Agent Record

### Agent Model Used

Claude Opus 5.5 (claude-opus-5-5), bmad-dev-story, 2026-11-02.

### Debug Log References

**Red-green.** The ATDD run had already seen two scaffolds red for the stated reason (the unmapped route's `404 not-available`). This phase wrote the contracts, the token index, `InvitationScope`, the store, the pipeline step and the endpoints before activating, so each scaffold went green on activation; the red half of each was then shown by its plant (table below), restored in a `finally` by a scratchpad runner that saved each file's text in memory. No `git checkout`, `restore` or `reset` was used. `git status` and a grep for the planted text confirmed every file restored.

| Plant | Expected red | Seen |
|---|---|---|
| 1 The view sets `State = Accepted` | AC1 safe-read case | RED `Viewing_an_Invitation_writes_nothing_and_only_the_accept_changes_anything` (the view then reads nothing through `Actionable` and answers 404; the unit's rollback also discards the write) |
| 2 Accept without the `Actionable` guard (update only) | race 2; expired/revoked arms | RED race 2 (`An_accept_racing_a_revoke_that_commits_under_it_...`); the five-state case stayed green because the screen still composes `Actionable` - race 2 is the real guard for the statement's own guard |
| 3 Accept as load, C# check, `SaveChanges` | race 1 or 2 | RED race 2; race 1 green (the lock serialises it) - race 2 is the guard |
| 4 Accept without the address clause (screen and update) | AC7 accept case | first pass refused at compile by S1172; second pass with analyzers silenced in the planted file: RED `A_different_Account_accepting_is_refused_403_...` |
| 5 Accept without the Invitations lock (screen, accept, bound count) | race 3 | RED race 3 ("answered while held"), race 1 and the bound race |
| 6a Bound count skipped | AC9 at 100 | (second pass) RED `The_101st_Membership_is_refused_...` |
| 6b Bound checked before the guard | full-Space ordering | RED `In_a_full_Space_a_stranger_or_a_dead_token_never_learns_the_Space_is_full` |
| 7 Unrecognised token returns early (distinct answer) | AC6 byte case; duration oracle | RED `Five_states_are_one_answer_...`; the permanent oracle `The_duration_method_tells_the_not_valid_answer_from_an_arm_that_does_no_work` is green (caught) on both surfaces in the isolation run - not masked |
| 8 View body gains `SpaceId` | response shape | RED `The_view_answers_exactly_the_Space_the_Role_and_the_issuer_...`; the contract gate holds the type |
| 9 Mismatch copy names an address | AC7 client case | RED `A_different_Account_is_told_without_either_address_...` |
| 10 Client sends the accept on load | AC4 client case | RED `Opening_the_link_sends_no_accept_and_clicking_Join_sends_exactly_one` |
| 11 No Session-only marker on the accept | no-Session/Token case | (second pass) RED wire `An_accept_with_no_Session_or_with_a_bearer_Token_...` (the anonymous 401 falls behind anti-forgery's 400) and RED route gate. A Token still gets no Membership: the step's own Session check fails closed |
| 12 A second `SpaceId`-key setter | setter gate | (second pass) RED `The_SpaceId_key_is_set_by_exactly_the_named_units_of_work` |
| 13 State write moved to another type | state-writer gate | (second pass) RED `Only_the_Invitation_store_writes_an_Invitation_state` |
| 14 The view's own `ExpiresAt >` | predicate gate | (second pass) RED `Only_the_shared_actionable_predicate_compares_ExpiresAt` |
| 15 noindex removed from `staticwebapp.config.json` | config gate | RED `The_static_host_sends_noindex_and_no_referrer_for_invitation_pages` |
| 16 The token logged | I-17 case | RED `Neither_the_token_nor_an_address_nor_a_name_reaches_the_Host_log_...` |
| 17 Acceptance handler reaches `NewSpace` | AD-22 slice gate | first pass (`_ = typeof(NewSpace)`) GREEN - masked: that form left nothing the gate's walk sees; second pass (a call to `NewSpace.Provision`) RED `The_acceptance_slice_never_provisions_...`. Recorded: the gate sees calls and signatures, not a bare `typeof` |

**Scaffolds corrected, and why.**
- `RefusalBoundaryGateTests.The_acceptance_unit_of_work_is_reached_only_by_its_port_its_registration_and_the_pipeline_step`: the scaffold asserted a `Yello.Host` dependent of the *concrete* unit, which is internal to Infrastructure (the ring forbids the Host naming it). Corrected to the stronger shape: the concrete `InvitationScope` is named only by itself and its registration; its port `IInvitationScope` is reached only by the Host's pipeline step (`ActiveSpaceResolution`). The registration is `SpaceContentStores`, not `InfrastructureServices` (S1200 holds that class at its limit).
- `DesignFoundationGateTests.No_component_declares_more_padding_than_the_invitation_view`: two existing rules pad 36px and are not components - the page shell `.auth-surface` and the Board's `.board-column--skeleton`. Named as exclusions with reasons rather than giving the view `spacing.7`, which `DESIGN.md:275` does not.
- `DesignFoundationGateTests.The_invitation_page_has_a_fixed_title_and_a_noindex_meta`: the scaffold required `<PageTitle>Yello</PageTitle>`, but story 1.7's standing gate `SpaceSwitcherGateTests.No_client_code_sets_the_document_title` forbids any client title setter (UX-DR33) and the host page's title is already exactly `Yello`. Corrected to assert no `PageTitle`; the 1.7 gate's detector now admits a `HeadContent` holding only the robots noindex meta (its detector test gained both directions).
- `InvitationViewTests.Registering_from_the_view_chains_register_then_sign_in_then_accept`: the label is "Your name" (`DisplayNameLabel`, as `Register.razor` renders it), not "Display name".
- Two existing gates widened by name, each with its reason: `No_endpoint_without_spaceId_in_its_path_reaches_a_Space_scoped_entity` (the view and accept, C7) and `The_endpoints_unreachable_with_a_Token_are_Token_management_the_switcher_and_the_move` (the accept's Session-only marker). `Space_is_read_only_where_named` names `InvitationStore` (the view's Space-name read). `SchemaVarianceTests.NamedVariances` names `InvitationToken.OfferedSpaceId`.
- The five `InvitationWire` stand-ins were swapped for `InvitationRoutes.Offer`/`.Acceptance`/`.TokenHeader` and `ProblemTypes.InvitationNotValid`/`.InvitationForAnotherAccount` in every use and deleted; the arch gate's two route patterns are kept as literals on purpose.

**Analyzer-driven shape (S1200).** The pipeline step is two nested types (`InvitationResolution`, `InvitationIdempotency`) inside `ActiveSpaceResolution`; the view/accept endpoints are their own class (`InvitationAcceptanceEndpoints`); the client page's requests live in `InvitationCalls`. The Session-only `401` for a route without `{spaceId}` is answered in `AccountScopedResolution.EstablishAsync` (the filter already ahead of anti-forgery), reusing story 1.8's existing `SessionOnlyEndpoint` marker - the accept is not an Account-scoped surface and establishes no context there.

**Iteration runs.** Slices (21 new wire cases + 6 handler cases), Isolation (307/307, the nine 4.3 rows live on both surfaces, timing row and oracle green), Architecture (437/437), Client (`InvitationViewTests` 10/10, the text-spacing theory 2/2): all green before the full run.

**Full run (the last thing run before review):** `docker info` 29.5.3; `dotnet build Yello.slnx` 0 warnings, 0 errors; `dotnet build-server shutdown`; `dotnet test Yello.slnx --no-build --max-parallel-test-modules 1`: **2,055 total, 2,045 passed, 0 failed, 10 skipped** (baseline 2,029 / 1,971 / 0 / 58: the 48 story-4.3 scaffolds are all live, plus 6 handler cases and the rows and theories they expand to; the 10 skips are the pre-existing ones, three of them the migration-bundle tests).

### Completion Notes List

- Ultimate context engine analysis completed - comprehensive developer guide created (create-story, 2026-11-01). Seven open questions (Q1-Q7) are marked [LEE] and must be decided before development.
- **Built (FR-11, AR-30/AD-25/NFR-8, AR-32/AD-28, AR-27/AD-22, AR-31/AD-27, UX-DR18, UX-DR35).** `GET /api/v1/invitations/offer` (token in `Yello-Invitation-Token`) and `POST /api/v1/invitations/acceptance` (`{ invitationToken }`, Session-only). The token is hashed and resolved through the policy-free `InvitationToken` index (written in the issue's save, cascading from the Invitation, backfilled by migration `20261102004259_InvitationTokenIndex`); `InvitationScope` opens one transaction and sets the `SpaceId` key alone to the stored `OfferedSpaceId` (a server-generated sentinel for anything unrecognised). The view reads through `OfferPredicates.Actionable`, never commits. The accept: idempotency replay, the store's screen under the Space's Invitations lock (actionable, addressed to the caller's own `Account` address, not already a Member), the Memberships-per-Space bound from the registry, then one guarded `ExecuteUpdate` (`Actionable` AND the caller's address) and one Membership at the row's Role; 200 `{ spaceId }`, uniform `404 invitation-not-valid`, `403 invitation-for-another-account`, `409 already-a-member`, `409 bound-reached`. Both 404s held to the not-found floor; nothing recorded as an `AccessRefusal`. Client `InvitationView.razor` at `/invitations/{Token}`; spine amendments to AD-2, AD-24 (C1 correction) and AD-28 dated 2026-11-02.
- **Lee's Q1-Q7 (a)** built as decided; no other choice relaxes isolation, RLS, a filter or a key. The migration's backfill switches the Space policy off inside its own transaction (story 2.10's `ListViewSortKeys` precedent) - migration-time only.
- Q8 - (a) the accept takes `Yello/Invitations/{spaceId}` first (screen, accept and the Memberships count). Cost: accepts and issues in one Space queue at one lock. Cut by reverting to a separate Memberships lock with a fixed order.
- Q9 - (a) lock, actionable, addressed, already-a-Member, bound, write; the pipeline asks the store's screen port (`IInvitationAcceptanceScreen`) before `CreationBound.ConsultAsync`. Cost: the accept reads the Invitation twice (screen, then its own guarded statement re-decides).
- Q10 - (a) `200 AcceptedInvitationResponse(SpaceId)`.
- Q11 - (a) `Idempotency-Key` honoured on the Space-scoped `IdempotencyStore` in the invited Space, keyed with the caller's Account; it works with `SpaceScope` narrowed but not resolved. Cost: the record goes with the Space if it is deleted.
- Q12 - (a) `409 already-a-member` (4.1's type), nothing written.
- Q13 - (a) `403 invitation-for-another-account`, unrecorded.
- Q14 - (a) no `MembershipChanged` publisher; ledgered to 7.7.
- Q15 - (a) the listed copy, plus "{Role}: {meaning}." for the Role's one-line meaning (RoleCopy's `RolePermits*` phrases do not read as a sentence after the Role's name) and "This invitation could not be loaded." for a view with no usable answer; "Sign out" and "Cancel" reuse `SignOutLabel` and `InvitationCancel`.
- Q16 - (a) inline sign-in on the view; no return-URL parameter.
- Q17 - (a) "lapsed" has no stored form; covered by the derived-expired arm.
- Q18 - (a) the raw-SQL scan beside the IL gates, its reach stated in the gate's remarks.
- Q19 - (a) `SpaceDirectory.Forget`, then `SpaceNavigation.GoTo(SpaceHome)`; "You joined {Space}." is held in `SpaceNarration` and said by the Space's home.
- Reversible: the page sets no `PageTitle` (story 1.7's UX-DR33 gate wins; the host title is already "Yello"); only the robots meta is added through `HeadContent`. Cut by reverting the 1.7 detector's one allowance.
- Reversible: state changes are said through one polite `role="status"` region present from the offer's first render, and a new state's heading takes focus - not `LiveAnnouncement`, whose hidden copy would duplicate the visible "Not sent." and bound sentences.
- Reversible: `InvitationToken` is keyed on `InvitationId` (one token per Invitation), `TokenHash` unique.
- Reversible: Lee's Q3 (a) "registration's files do not change" was read as including their comments, so `AccountRegistrationStore` and `RegisterAccountHandler` were not touched by Task 9's comment pass.
- Recorded behaviour (AC3): an address already registered answers registration's uniform `204` and then sign-in's ordinary "Email or password is incorrect." on the view - direct registration's existing behaviour, not a new oracle. An accept refused after registration leaves an Account with its Personal Space and no invited Membership (ledgered to UX).
- `staticwebapp.config.json` route headers follow the SWA schema but were not checked against live documentation or a deployed app (no web check); the Host does not serve the client, so no Development-time header was added (ledgered to Lee).

### File List

- `_bmad-output/implementation-artifacts/4-3-accept-an-invitation.md`
- `_bmad-output/implementation-artifacts/deferred-work.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`
- `_bmad-output/planning-artifacts/architecture/architecture-YelloBMAD-2026-08-17/ARCHITECTURE-SPINE.md`
- `tests/Yello.Tests.Architecture/DesignFoundationGateTests.InvitationView.cs`
- `tests/Yello.Tests.Architecture/RefusalBoundaryGateTests.cs`
- `tests/Yello.Tests.Architecture/RefusalBoundaryGateTests.InvitationAcceptance.cs`
- `tests/Yello.Tests.Architecture/RefusalBoundaryGateTests.InvitationRevoke.cs`
- `tests/Yello.Tests.Architecture/RefusalBoundaryGateTests.Move.cs`
- `tests/Yello.Tests.Architecture/SpaceSwitcherGateTests.cs`
- `tests/Yello.Tests.Architecture/SpaceSwitcherGateTests.Invitation.cs`
- `tests/Yello.Tests.Client/FakeYelloApi.cs`
- `tests/Yello.Tests.Client/FakeYelloApi.InvitationAcceptance.cs`
- `tests/Yello.Tests.Client/InvitationViewTests.cs`
- `tests/Yello.Tests.Client/TextSpacingTests.cs`
- `tests/Yello.Tests.Isolation/IsolationCases.InvitationAcceptance.cs`
- `tests/Yello.Tests.Isolation/IsolationTimingTests.cs`
- `tests/Yello.Tests.Isolation/SchemaVarianceTests.cs`
- `tests/Yello.Tests.Shared/InvitationWire.cs`
- `tests/Yello.Tests.Slices/Invitations/AcceptInvitation/ViewAndAcceptInvitationHandlerTests.cs`
- `tests/Yello.Tests.Slices/Invitations/IssueInvitation/IssueInvitationHandlerTests.cs`
- `tests/Yello.Tests.Slices/Invitations/RevokeInvitation/ListAndRevokeInvitationHandlerTests.cs`
- `tests/Yello.Tests.Slices/Spaces/ResolveActiveSpace/ActiveSpaceEndpointTests.AcceptInvitation.cs`
- `tests/Yello.Tests.Slices/Spaces/ResolveActiveSpace/ActiveSpaceEndpointTests.AcceptInvitationBound.cs`
- `tests/Yello.Tests.Slices/Spaces/ResolveActiveSpace/ActiveSpaceEndpointTests.AcceptInvitationRace.cs`
- `tests/Yello.Tests.Slices/Spaces/ResolveActiveSpace/ActiveSpaceEndpointTests.ViewInvitation.cs`
- `Yello.Application/Invitations/AcceptInvitation/AcceptInvitationHandler.cs`
- `Yello.Application/Invitations/IInvitationAcceptanceScreen.cs`
- `Yello.Application/Invitations/IInvitationScope.cs`
- `Yello.Application/Invitations/IInvitationStore.cs`
- `Yello.Application/Invitations/InvitationLinks.cs`
- `Yello.Application/Invitations/ViewInvitation/ViewInvitationHandler.cs`
- `Yello.Client/Localisation/ClientCopy.resx`
- `Yello.Client/Pages/ClientRoutes.cs`
- `Yello.Client/Pages/InvitationCalls.cs`
- `Yello.Client/Pages/InvitationView.razor`
- `Yello.Client/Pages/InvitationView.razor.cs`
- `Yello.Client/Program.cs`
- `Yello.Client/Refusals/RefusalCopy.cs`
- `Yello.Client/Sessions/SessionState.cs`
- `Yello.Client/wwwroot/css/components.css`
- `Yello.Client/wwwroot/staticwebapp.config.json`
- `Yello.Contracts/Invitations/AcceptedInvitationResponse.cs`
- `Yello.Contracts/Invitations/AcceptInvitationRequest.cs`
- `Yello.Contracts/Invitations/InvitationOfferResponse.cs`
- `Yello.Contracts/Invitations/InvitationRoutes.cs`
- `Yello.Contracts/ProblemTypes.cs`
- `Yello.Domain/Bounds/BoundRegistry.cs`
- `Yello.Domain/Invitations/InvitationState.cs`
- `Yello.Host/ApiRouteGroup.cs`
- `Yello.Host/Endpoints/InvitationAcceptanceEndpoints.cs`
- `Yello.Host/Endpoints/InvitationEndpoints.cs`
- `Yello.Host/Program.cs`
- `Yello.Host/Spaces/AccountScopedResolution.cs`
- `Yello.Host/Spaces/ActiveSpaceResolution.Invitation.cs`
- `Yello.Host/Spaces/NotAvailableFloor.cs`
- `Yello.Infrastructure/Invitations/InvitationTokenSecret.cs`
- `Yello.Infrastructure/Persistence/BoundCounter.cs`
- `Yello.Infrastructure/Persistence/Configurations/InvitationConfiguration.cs`
- `Yello.Infrastructure/Persistence/Configurations/InvitationModel.cs`
- `Yello.Infrastructure/Persistence/Configurations/InvitationTokenConfiguration.cs`
- `Yello.Infrastructure/Persistence/InvitationScope.cs`
- `Yello.Infrastructure/Persistence/InvitationStore.cs`
- `Yello.Infrastructure/Persistence/InvitationToken.cs`
- `Yello.Infrastructure/Persistence/Migrations/20261102004259_InvitationTokenIndex.cs`
- `Yello.Infrastructure/Persistence/Migrations/20261102004259_InvitationTokenIndex.Designer.cs`
- `Yello.Infrastructure/Persistence/Migrations/YelloDbContextModelSnapshot.cs`
- `Yello.Infrastructure/Persistence/OfferPredicates.cs`
- `Yello.Infrastructure/Persistence/SchemaNames.cs`
- `Yello.Infrastructure/SpaceContentStores.cs`

## Change Log

- 2026-11-02 - Story 4.3 implemented (bmad-dev-story): the Invitation view and accept on the policy-free token index and the `SpaceId`-only Invitation unit of work (Lee's Q1-Q7 (a)); Q8-Q19 decided at their recommendations; spine amendments to AD-2, AD-24 and AD-28; all 48 ATDD scaffolds and nine isolation rows live; 17 plants seen red; ledger dispositions applied. Status: review.
