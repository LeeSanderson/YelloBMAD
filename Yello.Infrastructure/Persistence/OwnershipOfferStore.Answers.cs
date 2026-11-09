using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Yello.Application.Spaces;
using Yello.Domain.Memberships;
using Yello.Domain.Spaces;

namespace Yello.Infrastructure.Persistence;

/// <summary>
/// The recipient's side of the offer: its read, its acceptance and its decline, each decided by row identity.
/// </summary>
internal sealed partial class OwnershipOfferStore
{
    /// <inheritdoc />
    public async Task<OwnershipOfferForRecipient?> ReadMineAsync(Guid actorAccountId, CancellationToken cancellationToken)
    {
        var spaceId = ScopedSpaceId();

        return await dbContext.Set<OwnershipOffer>()
            .AsNoTracking()
            .Where(OfferPredicates.OwnershipOfferActionable)
            .Where(offer => dbContext.Memberships.Any(recipient =>
                recipient.Id == offer.RecipientMembershipId && recipient.SpaceId == spaceId && recipient.AccountId == actorAccountId))
            .Select(offer => new OwnershipOfferForRecipient(offer.Id, offer.OffererName, offer.ExpiresAt, DatabaseClock.Now()))
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// <b>Two explicit <c>ExecuteUpdate</c> statements, demote then promote, never tracked entities and
    /// <c>SaveChanges</c>.</b> EF Core picks its own order for two tracked rows, and SQL Server has no deferred constraint
    /// checking: promoting first would transiently write a second <c>Owner</c> row, which the filtered unique index refuses.
    /// Demoting first leaves zero rows in that index, and zero never collides. Both run in the request's one transaction,
    /// so no reader ever sees the moment between them.
    /// </para>
    /// <para>
    /// <b>Under the Memberships lock</b>, which every Role write, removal, leave, offer and revoke takes, so nothing lands
    /// between the two statements.
    /// </para>
    /// </remarks>
    public async Task<OwnershipOfferAcceptance> AcceptAsync(Guid actorAccountId, Guid offerId, CancellationToken cancellationToken)
    {
        await ApplicationLocks.TakeMembershipsAsync(dbContext, cancellationToken).ConfigureAwait(false);

        var spaceId = ScopedSpaceId();

        var accepted = await NamingTheCaller(spaceId, actorAccountId, offerId)
            .Where(OfferPredicates.OwnershipOfferActionable)
            .ExecuteUpdateAsync(setters => setters.SetProperty(offer => offer.State, OwnershipOfferState.Accepted), cancellationToken)
            .ConfigureAwait(false);

        if (accepted != 1)
        {
            return await MissAsync(spaceId, actorAccountId, offerId, cancellationToken).ConfigureAwait(false) switch
            {
                OwnershipOfferDecline.LostMembership => OwnershipOfferAcceptance.LostMembership,
                OwnershipOfferDecline.NotPending => OwnershipOfferAcceptance.NotPending,
                _ => OwnershipOfferAcceptance.NotFound,
            };
        }

        return await SwapAsync(spaceId, actorAccountId, offerId, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// The accepted offer's Role swap: demote its offerer, then promote the caller, each by exactly one row, then record
    /// both Accounts' change.
    /// </summary>
    private async Task<OwnershipOfferAcceptance> SwapAsync(Guid spaceId, Guid actorAccountId, Guid offerId, CancellationToken cancellationToken)
    {
        var offered = await dbContext.Set<OwnershipOffer>()
            .AsNoTracking()
            .Where(offer => offer.Id == offerId)
            .Select(offer => new { offer.OfferedByMembershipId, offer.RecipientMembershipId })
            .SingleAsync(cancellationToken)
            .ConfigureAwait(false);

        var formerOwnerAccountId = await dbContext.Memberships
            .AsNoTracking()
            .Where(membership => membership.SpaceId == spaceId && membership.Id == offered.OfferedByMembershipId)
            .Select(membership => membership.AccountId)
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        // The offerer is the Owner by construction; a miss means the terms the offer showed no longer hold, and the refusal
        // rolls back its Accepted with the unit of work. Never a promotion without a demotion.
        var demoted = await dbContext.Memberships
            .Where(membership => membership.SpaceId == spaceId && membership.Id == offered.OfferedByMembershipId && membership.Role == Role.Owner)
            .ExecuteUpdateAsync(setters => setters.SetProperty(membership => membership.Role, Role.Admin), cancellationToken)
            .ConfigureAwait(false);

        if (demoted != 1)
        {
            return OwnershipOfferAcceptance.OfferStale;
        }

        int promoted;

        try
        {
            promoted = await dbContext.Memberships
                .Where(membership => membership.SpaceId == spaceId
                    && membership.Id == offered.RecipientMembershipId
                    && membership.AccountId == actorAccountId
                    && membership.Role != Role.Owner)
                .ExecuteUpdateAsync(setters => setters.SetProperty(membership => membership.Role, Role.Owner), cancellationToken)
                .ConfigureAwait(false);
        }
        catch (SqlException exception) when (exception.Number is UniqueIndexViolationErrorNumber or UniqueConstraintViolationErrorNumber)
        {
            // A second Owner appeared between the two statements. No product write can do that under the lock, but the
            // answer is stable rather than a 500, and the unit of work rolls everything back.
            return OwnershipOfferAcceptance.OwnerRoleFixed;
        }

        if (promoted != 1)
        {
            return OwnershipOfferAcceptance.OfferStale;
        }

        // Two Roles moved, and a lease carries the Role: one publish per affected Account, after the commit only.
        membershipChanges.Record(formerOwnerAccountId, spaceId);
        membershipChanges.Record(actorAccountId, spaceId);

        return OwnershipOfferAcceptance.Accepted;
    }

    /// <inheritdoc />
    public async Task<OwnershipOfferDecline> DeclineAsync(Guid actorAccountId, Guid offerId, CancellationToken cancellationToken)
    {
        await ApplicationLocks.TakeMembershipsAsync(dbContext, cancellationToken).ConfigureAwait(false);

        var spaceId = ScopedSpaceId();

        var declined = await NamingTheCaller(spaceId, actorAccountId, offerId)
            .Where(OfferPredicates.OwnershipOfferActionable)
            .ExecuteUpdateAsync(setters => setters.SetProperty(offer => offer.State, OwnershipOfferState.Declined), cancellationToken)
            .ConfigureAwait(false);

        return declined == 1
            ? OwnershipOfferDecline.Declined
            : await MissAsync(spaceId, actorAccountId, offerId, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// The offer <paramref name="offerId"/> only while it names the caller's own Membership in this Space: the identity rule,
    /// in the statement's own <c>WHERE</c>.
    /// </summary>
    private IQueryable<OwnershipOffer> NamingTheCaller(Guid spaceId, Guid actorAccountId, Guid offerId) =>
        dbContext.Set<OwnershipOffer>()
            .Where(offer => offer.Id == offerId
                && dbContext.Memberships.Any(recipient =>
                    recipient.Id == offer.RecipientMembershipId && recipient.SpaceId == spaceId && recipient.AccountId == actorAccountId));

    /// <summary>
    /// Why a guarded answer wrote nothing: the caller's Membership is gone; the offer names the caller but is no longer
    /// actionable; or no offer with this id names the caller here - which answers alike for an unknown id, another Space's,
    /// and one naming someone else, so nothing is disclosed about whom an offer names.
    /// </summary>
    private async Task<OwnershipOfferDecline> MissAsync(Guid spaceId, Guid actorAccountId, Guid offerId, CancellationToken cancellationToken)
    {
        var own = await dbContext.Memberships
            .AsNoTracking()
            .Where(membership => membership.SpaceId == spaceId && membership.AccountId == actorAccountId)
            .Select(membership => new { membership.Id })
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (own is null)
        {
            return OwnershipOfferDecline.LostMembership;
        }

        var membershipId = own.Id;

        var named = await dbContext.Set<OwnershipOffer>()
            .AsNoTracking()
            .AnyAsync(offer => offer.Id == offerId && offer.RecipientMembershipId == membershipId, cancellationToken)
            .ConfigureAwait(false);

        return named ? OwnershipOfferDecline.NotPending : OwnershipOfferDecline.NotFound;
    }
}
