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
}
