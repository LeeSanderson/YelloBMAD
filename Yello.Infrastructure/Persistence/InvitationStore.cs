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
/// with a rowcount check. Under RCSI the <c>UPDATE</c> waits on an uncommitted writer of the row (4.3's acceptance, or a
/// second revoke) and re-evaluates its guard against what that writer committed, so it can never overwrite an
/// acceptance. No application lock and no <c>HOLDLOCK</c>. A revoke committing between an issue's actionable-pending
/// read and its insert can only make that issue answer <c>invitation-pending</c> for an Invitation just revoked - benign,
/// visible, and a retry succeeds. It is an <c>UPDATE</c>, never a <c>DELETE</c>: the row keeps its terminal state
/// (FR-10). On a missed update it reads only whether the id <i>exists</i> in this Space, to choose between
/// <c>NotPending</c> and <c>NotFound</c>; it returns no field of the terminal row. <c>ExecuteUpdate</c> tracks nothing, so
/// the pipeline's idempotency record saves cleanly after it.
/// </para>
/// <para>
/// <b>The only writer of <c>Invitation.State</c></b> (A-10, AC11): the issue's insert of <c>Pending</c> and the revoke. A
/// gate reads that from the IL; 4.3 adds its accept.
/// </para>
/// </remarks>
/// <param name="dbContext">The request's unit of work.</param>
/// <param name="identifiers">Produces the Invitation's identity (AR-34).</param>
/// <param name="clock">Stamps the issue instant.</param>
internal sealed class InvitationStore(YelloDbContext dbContext, IIdentifierGenerator identifiers, TimeProvider clock)
    : IInvitationStore
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

        var invitation = new Invitation
        {
            Id = identifiers.Next(),
            SpaceId = spaceId,
            EmailAddress = emailAddress.Trim(),
            NormalizedEmailAddress = normalized,
            Role = role,
            State = InvitationState.Pending,
            TokenHash = InvitationTokenSecret.Hash(token),
            IssuedByAccountId = issuerAccountId,
            IssuerName = issuerName,
            CreatedAt = clock.GetUtcNow(),
        };

        dbContext.Set<Invitation>().Add(invitation);

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
}
