using Microsoft.EntityFrameworkCore;
using Yello.Application.Invitations;
using Yello.Domain;
using Yello.Domain.Accounts;
using Yello.Domain.Invitations;
using Yello.Domain.Memberships;
using Yello.Infrastructure.Invitations;

namespace Yello.Infrastructure.Persistence;

/// <summary>
/// Issues Invitations inside the request's Space-scoped unit of work. Story 4.1; FR-10, AD-23, AD-27.
/// </summary>
/// <remarks>
/// <para>
/// <b>The order is the decision</b> (the story's Locking section). It takes the Space's Invitations application lock
/// (<see cref="ApplicationLocks.InvitationsOf"/>), then reads - plain statements, which under
/// <c>READ_COMMITTED_SNAPSHOT</c> see every issue committed before the lock was granted - whether the address already
/// holds a Membership here (AC4) and whether it already holds an actionable pending Invitation here (AC13, through the
/// one shared predicate, <see cref="OfferPredicates.Actionable"/>), and only then inserts. A concurrent issue of the same
/// address waits on the lock and then sees this one. No <c>HOLDLOCK</c> anywhere (story 3.4).
/// </para>
/// <para>
/// <b>No <c>Where</c> on <c>SpaceId</c></b>: both isolation layers are already narrowed to the request's Space. The
/// Membership read joins the policy-free <c>Account</c> table on its unique normalised address - it asks whether a
/// Membership <i>in this Space</i> carries the address, never whether an Account exists anywhere (AD-23); the plan is the
/// same seek either way.
/// </para>
/// <para>
/// <b>The expiry is the database's.</b> The insert names no expiry; the column default stamps it from
/// <c>SYSDATETIMEOFFSET()</c> and EF Core reads it back onto the entity.
/// </para>
/// <para>
/// <b>Saves its own effect</b>, as every store does (<see cref="IdempotencyStore"/>).
/// </para>
/// <para>
/// <b>The list</b> (story 4.2, AC1, AC6) reads through <see cref="OfferPredicates.Actionable"/>, newest first, and projects
/// exactly the five columns it answers - never the entity, so the token hash is not read.
/// </para>
/// <para>
/// <b>The revoke</b> (story 4.2, AC2, AC4, AC5; open question 10 (a)) is one guarded statement,
/// <c>UPDATE Invitation SET State = 'Revoked' WHERE Id = @id AND State = 'Pending' AND ExpiresAt &gt; SYSDATETIMEOFFSET()</c>,
/// with a rowcount check. Under RCSI the <c>UPDATE</c> waits on an uncommitted writer of the row (story 4.3's accept, or a
/// second revoke) and re-evaluates its guard against what that writer committed, so it can never overwrite an
/// acceptance. No application lock and no <c>HOLDLOCK</c>. A revoke committing between an issue's actionable-pending
/// read and its insert can only make that issue answer <c>invitation-pending</c> for an Invitation just revoked - benign,
/// visible, and a retry succeeds. It is an <c>UPDATE</c>, never a <c>DELETE</c>: the row keeps its terminal state
/// (FR-10). On a missed update it reads only whether the id <i>exists</i> in this Space, to choose between
/// <c>NotPending</c> and <c>NotFound</c>; it returns no field of the terminal row. <c>ExecuteUpdate</c> tracks nothing, so
/// the pipeline's idempotency record saves cleanly after it.
/// </para>
/// <para>
/// <b>The only writer of <c>Invitation.State</c></b> (A-10, AC11): the issue's insert of <c>Pending</c>, the revoke, and
/// story 4.3's accept - the three sanctioned writes. A gate reads that from the IL.
/// </para>
/// <para>
/// <b>The token index</b> (story 4.3, Lee's Q1 (a)): the issue writes its <see cref="InvitationToken"/> row in the same
/// save as the Invitation, so a token exists in the index exactly when its Invitation does. This store writes the index
/// and never reads it; <see cref="InvitationScope"/> is its one reader.
/// </para>
/// <para>
/// <b>The view and the accept</b> (story 4.3) run on <see cref="InvitationScope"/>'s unit of work, under the <c>SpaceId</c>
/// key the token resolved to. The view reads the offer through <see cref="OfferPredicates.Actionable"/> - the Space's name
/// from the policy-free <c>Space</c> row the Invitation names, the Role, the captured issuer name - and writes nothing.
/// The accept takes the Space's Invitations lock (open question 8 (a)), screens - actionable, addressed to the caller,
/// not already a Member, in that order (open question 9 (a)) - and then runs <b>one guarded statement</b>,
/// <c>UPDATE Invitation SET State = 'Accepted' WHERE Id = @id AND State = 'Pending' AND ExpiresAt &gt; SYSDATETIMEOFFSET()
/// AND NormalizedEmailAddress = @caller</c>, with a rowcount check; only on a rowcount of one does it insert one
/// Membership, at the Role and in the Space the row names. The caller's address is read from the caller's own
/// <c>Account</c> row by the Session's Account id - never from the request - so the token is never the authority (AD-28).
/// Under RCSI a revoke uncommitted on the row makes the <c>UPDATE</c> wait and re-evaluate its guard against what the
/// revoke committed, so an acceptance can never overwrite a revocation, or the reverse.
/// </para>
/// </remarks>
/// <param name="dbContext">The request's unit of work.</param>
/// <param name="identifiers">Produces the Invitation's identity (AR-34).</param>
/// <param name="clock">Stamps the issue instant.</param>
internal sealed class InvitationStore(YelloDbContext dbContext, IIdentifierGenerator identifiers, TimeProvider clock)
    : IInvitationStore, IInvitationAcceptanceScreen
{
    /// <inheritdoc />
    public async Task<InvitationIssue> IssueAsync(
        Guid spaceId,
        Guid issuerAccountId,
        string emailAddress,
        Role role,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(emailAddress);

        await ApplicationLocks.TakeInvitationsAsync(dbContext, cancellationToken).ConfigureAwait(false);

        var normalized = EmailAddressNormalisation.Normalise(emailAddress);

        var alreadyAMember = await dbContext.Memberships
            .AsNoTracking()
            .Join(dbContext.Accounts, membership => membership.AccountId, account => account.Id, (_, account) => account.NormalizedEmailAddress)
            .AnyAsync(address => address == normalized, cancellationToken)
            .ConfigureAwait(false);

        if (alreadyAMember)
        {
            return InvitationIssue.AlreadyAMember;
        }

        var alreadyPending = await dbContext.Set<Invitation>()
            .AsNoTracking()
            .Where(OfferPredicates.Actionable)
            .AnyAsync(invitation => invitation.NormalizedEmailAddress == normalized, cancellationToken)
            .ConfigureAwait(false);

        if (alreadyPending)
        {
            return InvitationIssue.AlreadyPending;
        }

        // The attribution captured at issue (epics.md:1793): 4.2's list shows it with no live Account lookup.
        var issuerName = await dbContext.Accounts
            .AsNoTracking()
            .Where(account => account.Id == issuerAccountId)
            .Select(account => account.DisplayName)
            .SingleAsync(cancellationToken)
            .ConfigureAwait(false);

        var token = InvitationTokenSecret.Generate();
        var tokenHash = InvitationTokenSecret.Hash(token);

        var invitation = new Invitation
        {
            Id = identifiers.Next(),
            SpaceId = spaceId,
            EmailAddress = emailAddress.Trim(),
            NormalizedEmailAddress = normalized,
            Role = role,
            State = InvitationState.Pending,
            TokenHash = tokenHash,
            IssuedByAccountId = issuerAccountId,
            IssuerName = issuerName,
            CreatedAt = clock.GetUtcNow(),
        };

        dbContext.Set<Invitation>().Add(invitation);

        // Story 4.3's token index, in the same save: the view and the accept resolve the token to this Space through it.
        dbContext.Set<InvitationToken>().Add(new InvitationToken
        {
            TokenHash = tokenHash,
            InvitationId = invitation.Id,
            OfferedSpaceId = spaceId,
        });

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return InvitationIssue.Issued(invitation, token);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<PendingInvitation>> ListPendingAsync(CancellationToken cancellationToken) =>
        await dbContext.Set<Invitation>()
            .AsNoTracking()
            .Where(OfferPredicates.Actionable)
            .OrderByDescending(invitation => invitation.CreatedAt)
            .ThenByDescending(invitation => invitation.Id)
            .Select(invitation => new PendingInvitation(
                invitation.Id,
                invitation.EmailAddress,
                invitation.Role,
                invitation.IssuerName,
                invitation.CreatedAt))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    /// <inheritdoc />
    public async Task<InvitationRevocation> RevokeAsync(Guid invitationId, CancellationToken cancellationToken)
    {
        var revoked = await dbContext.Set<Invitation>()
            .Where(invitation => invitation.Id == invitationId)
            .Where(OfferPredicates.Actionable)
            .ExecuteUpdateAsync(setters => setters.SetProperty(invitation => invitation.State, InvitationState.Revoked), cancellationToken)
            .ConfigureAwait(false);

        if (revoked == 1)
        {
            return InvitationRevocation.Revoked;
        }

        // Existence only, under both isolation layers: another Space's row is invisible, so it answers as one that never
        // existed (open question 3 (a)).
        var exists = await dbContext.Set<Invitation>()
            .AsNoTracking()
            .AnyAsync(invitation => invitation.Id == invitationId, cancellationToken)
            .ConfigureAwait(false);

        return exists ? InvitationRevocation.NotPending : InvitationRevocation.NotFound;
    }

    /// <inheritdoc />
    public Task<InvitationOffer?> ReadOfferAsync(InvitationContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        // Split for S4457.
        return ReadOfferCoreAsync(context, cancellationToken);
    }

    /// <inheritdoc />
    public Task<InvitationAcceptanceOutcome> ScreenAsync(InvitationContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        // Split for S4457.
        return ScreenCoreAsync(context, AccountOf(context), cancellationToken);
    }

    /// <inheritdoc />
    public Task<InvitationAcceptanceOutcome> AcceptAsync(InvitationContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        // Split for S4457.
        return AcceptCoreAsync(context, AccountOf(context), cancellationToken);
    }

    private static Guid AccountOf(InvitationContext context) =>
        context.AccountId ?? throw new InvalidOperationException("An Invitation is accepted only by a signed-in Account.");

    private async Task<InvitationOffer?> ReadOfferCoreAsync(InvitationContext context, CancellationToken cancellationToken)
    {
        // With a Session, read whatever the token names: the caller's address is read either way, so the work does not
        // depend on whether the token was recognised.
        var callerAddress = context.AccountId is { } accountId
            ? await CallerAddressAsync(accountId, cancellationToken).ConfigureAwait(false)
            : null;

        // No Where on SpaceId: both isolation layers are narrowed to the Space the token resolved to, and the policy-free
        // Space row is joined on the Invitation's own SpaceId - the one Space's name, read for its Invitation's offer.
        var offer = await dbContext.Set<Invitation>()
            .AsNoTracking()
            .Where(invitation => invitation.Id == context.InvitationId)
            .Where(OfferPredicates.Actionable)
            .Join(
                dbContext.Spaces,
                invitation => invitation.SpaceId,
                space => space.Id,
                (invitation, space) => new
                {
                    SpaceName = space.Name,
                    invitation.Role,
                    invitation.IssuerName,
                    Addressed = invitation.NormalizedEmailAddress == callerAddress,
                })
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (offer is null)
        {
            return null;
        }

        // The indicator reads a Session only (Lee's Q5 (a)): without one - a bearer Token included - it is absent.
        bool? addressedToYou = context.AccountId is null ? null : offer.Addressed;

        return new InvitationOffer(offer.SpaceName, offer.Role, offer.IssuerName, addressedToYou);
    }

    private async Task<InvitationAcceptanceOutcome> ScreenCoreAsync(
        InvitationContext context,
        Guid accountId,
        CancellationToken cancellationToken)
    {
        await ApplicationLocks.TakeInvitationsAsync(dbContext, cancellationToken).ConfigureAwait(false);

        var callerAddress = await CallerAddressAsync(accountId, cancellationToken).ConfigureAwait(false);

        return await ScreenUnderLockAsync(context, accountId, callerAddress, cancellationToken).ConfigureAwait(false);
    }

    private async Task<InvitationAcceptanceOutcome> AcceptCoreAsync(
        InvitationContext context,
        Guid accountId,
        CancellationToken cancellationToken)
    {
        // The lock first (open question 8 (a)): the pipeline's screen already holds it on this transaction, and taking it
        // again is a no-op for the same owner - it is taken here too so the accept never depends on its caller for it.
        await ApplicationLocks.TakeInvitationsAsync(dbContext, cancellationToken).ConfigureAwait(false);

        var callerAddress = await CallerAddressAsync(accountId, cancellationToken).ConfigureAwait(false);

        var screened = await ScreenUnderLockAsync(context, accountId, callerAddress, cancellationToken).ConfigureAwait(false);

        if (screened != InvitationAcceptanceOutcome.Accepted)
        {
            return screened;
        }

        // The one guarded statement (AD-27, AD-28): actionable, and addressed to the caller's own address - never
        // load-check-save. ExecuteUpdate tracks nothing, so the pipeline's idempotency record saves cleanly after it.
        var accepted = await dbContext.Set<Invitation>()
            .Where(invitation => invitation.Id == context.InvitationId)
            .Where(OfferPredicates.Actionable)
            .Where(invitation => invitation.NormalizedEmailAddress == callerAddress)
            .ExecuteUpdateAsync(setters => setters.SetProperty(invitation => invitation.State, InvitationState.Accepted), cancellationToken)
            .ConfigureAwait(false);

        if (accepted != 1)
        {
            // A revoke committed under the screen (it waited on the row and re-evaluated): read again what the token names
            // now. The address cannot have changed, so this is the uniform answer.
            var now = await ScreenUnderLockAsync(context, accountId, callerAddress, cancellationToken).ConfigureAwait(false);

            return now == InvitationAcceptanceOutcome.Accepted ? InvitationAcceptanceOutcome.NotValid : now;
        }

        // The Space and the Role come from the row, never from the request.
        var offered = await dbContext.Set<Invitation>()
            .AsNoTracking()
            .Where(invitation => invitation.Id == context.InvitationId)
            .Select(invitation => new { invitation.SpaceId, invitation.Role })
            .SingleAsync(cancellationToken)
            .ConfigureAwait(false);

        dbContext.Memberships.Add(new Membership
        {
            Id = identifiers.Next(),
            SpaceId = offered.SpaceId,
            AccountId = accountId,
            Role = offered.Role,
            CreatedAt = clock.GetUtcNow(),
        });

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return InvitationAcceptanceOutcome.Accepted;
    }

    /// <summary>
    /// What the accept comes to before it writes, in open question 9 (a)'s order: an Invitation that is not actionable is
    /// the uniform answer whoever asks; an actionable one addressed to someone else is the mismatch; a caller already a
    /// Member here is refused (open question 12 (a)); otherwise it would be admitted. Writes nothing.
    /// </summary>
    private async Task<InvitationAcceptanceOutcome> ScreenUnderLockAsync(
        InvitationContext context,
        Guid accountId,
        string? callerAddress,
        CancellationToken cancellationToken)
    {
        var actionable = await dbContext.Set<Invitation>()
            .AsNoTracking()
            .Where(invitation => invitation.Id == context.InvitationId)
            .Where(OfferPredicates.Actionable)
            .Select(invitation => new { IsTheCallers = invitation.NormalizedEmailAddress == callerAddress })
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (actionable is null)
        {
            return InvitationAcceptanceOutcome.NotValid;
        }

        if (!actionable.IsTheCallers)
        {
            return InvitationAcceptanceOutcome.ForAnotherAccount;
        }

        // This Space's Memberships only: both layers are narrowed to it, and no AccountId key is set.
        var alreadyAMember = await dbContext.Memberships
            .AsNoTracking()
            .AnyAsync(membership => membership.AccountId == accountId, cancellationToken)
            .ConfigureAwait(false);

        return alreadyAMember ? InvitationAcceptanceOutcome.AlreadyAMember : InvitationAcceptanceOutcome.Accepted;
    }

    /// <summary>
    /// The caller's own normalised address, from its <c>Account</c> row by the Session's Account id - the authority for an
    /// accept, and never anything the request carried. <c>null</c> for an Account that no longer exists.
    /// </summary>
    private Task<string?> CallerAddressAsync(Guid accountId, CancellationToken cancellationToken) =>
        dbContext.Accounts
            .AsNoTracking()
            .Where(account => account.Id == accountId)
            .Select(account => account.NormalizedEmailAddress)
            .SingleOrDefaultAsync(cancellationToken);
}
