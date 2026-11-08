using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Yello.Application.Spaces;
using Yello.Domain;
using Yello.Domain.Memberships;
using Yello.Domain.Spaces;

namespace Yello.Infrastructure.Persistence;

/// <summary>
/// Makes, reads and revokes the resolved Space's Ownership Offer inside the request's Space-scoped unit of work. FR-8, AD-26,
/// AD-27.
/// </summary>
/// <remarks>
/// <para>
/// <b>The offer and the revoke take the Space's Memberships lock first</b> (<see cref="ApplicationLocks.TakeMembershipsAsync"/>,
/// Exclusive), the lock a removal and a leave hold while they lapse an offer naming the departing Membership. So an offer and
/// the removal of its recipient serialise, and with no key from the offer to <c>Membership</c> that is what keeps a
/// <c>Pending</c> offer from naming a Membership that is gone. No <c>HOLDLOCK</c>.
/// </para>
/// <para>
/// <b>The offer is one guarded <c>INSERT ... SELECT</c></b>: it inserts only where the recipient is a non-Owner Membership
/// of this Space and the caller holds this Space's Owner Membership, so the rule is in the statement's own <c>WHERE</c> and
/// the slice reads no Role. Before it, under the same lock, any <c>Pending</c> offer of this Space whose expiry has passed is
/// written <c>Lapsed</c>: expiry itself is never written, so such a row would otherwise hold the filtered unique index
/// forever. A second pending offer is refused by that index, not by a read; its <c>2601</c> is answered
/// <see cref="OwnershipOfferOutcome.AlreadyPending"/>, never a <c>500</c>, and the unit of work rolls back.
/// </para>
/// <para>
/// <b>The revoke</b> is one guarded <c>UPDATE</c> composed with the shared actionable predicate and a rowcount check; on a
/// miss it reads only whether the id exists in this Space, to choose between <see cref="OwnershipOfferRevocation.NotPending"/>
/// and <see cref="OwnershipOfferRevocation.NotFound"/>. It is never a <c>DELETE</c>: offers are retained.
/// </para>
/// <para>
/// <b>The read</b> takes no lock and reads the database clock in the same statement, so the client counts days from the
/// server's time.
/// </para>
/// <para>
/// <b>With <see cref="OwnershipOfferWrites"/>, the only writer of <c>OwnershipOffer.State</c></b>; a gate reads that from
/// the IL. Raw SQL and <c>ExecuteUpdate</c> track nothing, so the pipeline's idempotency record saves cleanly after them.
/// </para>
/// </remarks>
/// <param name="dbContext">The request's unit of work.</param>
/// <param name="identifiers">Produces the offer's identity (AR-34).</param>
/// <param name="clock">Stamps the offer's instant.</param>
internal sealed class OwnershipOfferStore(YelloDbContext dbContext, IIdentifierGenerator identifiers, TimeProvider clock) : IOwnershipOfferStore
{
    private const int UniqueIndexViolationErrorNumber = 2601;

    private const int UniqueConstraintViolationErrorNumber = 2627;

    private const string OwnerRole = nameof(Role.Owner);

    private const string PendingState = nameof(OwnershipOfferState.Pending);

    /// <inheritdoc />
    public async Task<OwnershipOfferMade> OfferAsync(Guid actorAccountId, Guid recipientMembershipId, string offererName, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(offererName);

        await ApplicationLocks.TakeMembershipsAsync(dbContext, cancellationToken).ConfigureAwait(false);

        var spaceId = dbContext.ScopedSpaceId
            ?? throw new InvalidOperationException("An Ownership Offer is made only inside a Space-scoped unit of work.");

        await dbContext.Set<OwnershipOffer>()
            .Where(OfferPredicates.OwnershipOfferPendingButExpired)
            .ExecuteUpdateAsync(setters => setters.SetProperty(offer => offer.State, OwnershipOfferState.Lapsed), cancellationToken)
            .ConfigureAwait(false);

        var offerId = identifiers.Next();
        var createdAt = clock.GetUtcNow();
        int inserted;

        try
        {
            inserted = await dbContext.Database.ExecuteSqlAsync(
                $"""
                INSERT INTO dbo.[OwnershipOffer] (Id, SpaceId, RecipientMembershipId, OfferedByMembershipId, OffererName, State, CreatedAt)
                SELECT {offerId}, {spaceId}, recipient.Id, owner.Id, {offererName}, {PendingState}, {createdAt}
                FROM dbo.[Membership] AS recipient
                JOIN dbo.[Membership] AS owner
                  ON owner.SpaceId = {spaceId}
                 AND owner.AccountId = {actorAccountId}
                 AND owner.Role = {OwnerRole}
                WHERE recipient.Id = {recipientMembershipId}
                  AND recipient.SpaceId = {spaceId}
                  AND recipient.Role <> {OwnerRole};
                """,
                cancellationToken).ConfigureAwait(false);
        }
        catch (SqlException exception) when (exception.Number is UniqueIndexViolationErrorNumber or UniqueConstraintViolationErrorNumber)
        {
            return new OwnershipOfferMade(OwnershipOfferOutcome.AlreadyPending, null);
        }

        if (inserted == 1)
        {
            return new OwnershipOfferMade(OwnershipOfferOutcome.Offered, offerId);
        }

        // Whether the caller is still here decides between its own lost Membership and a recipient that is not one.
        var actorStillHere = await dbContext.Memberships
            .AsNoTracking()
            .AnyAsync(membership => membership.AccountId == actorAccountId, cancellationToken)
            .ConfigureAwait(false);

        return new OwnershipOfferMade(actorStillHere ? OwnershipOfferOutcome.InvalidRecipient : OwnershipOfferOutcome.LostMembership, null);
    }

    /// <inheritdoc />
    public async Task<PendingOwnershipOffer?> ReadPendingAsync(CancellationToken cancellationToken) =>
        await dbContext.Set<OwnershipOffer>()
            .AsNoTracking()
            .Where(OfferPredicates.OwnershipOfferActionable)
            .Select(offer => new PendingOwnershipOffer(offer.Id, offer.RecipientMembershipId, offer.ExpiresAt, DatabaseClock.Now()))
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

    /// <inheritdoc />
    public async Task<OwnershipOfferRevocation> RevokeAsync(Guid offerId, CancellationToken cancellationToken)
    {
        await ApplicationLocks.TakeMembershipsAsync(dbContext, cancellationToken).ConfigureAwait(false);

        var revoked = await dbContext.Set<OwnershipOffer>()
            .Where(offer => offer.Id == offerId)
            .Where(OfferPredicates.OwnershipOfferActionable)
            .ExecuteUpdateAsync(setters => setters.SetProperty(offer => offer.State, OwnershipOfferState.Revoked), cancellationToken)
            .ConfigureAwait(false);

        if (revoked == 1)
        {
            return OwnershipOfferRevocation.Revoked;
        }

        // Existence only, under both isolation layers: another Space's offer is invisible, so it answers as one that never
        // existed. No field of the row is read.
        var exists = await dbContext.Set<OwnershipOffer>()
            .AsNoTracking()
            .AnyAsync(offer => offer.Id == offerId, cancellationToken)
            .ConfigureAwait(false);

        return exists ? OwnershipOfferRevocation.NotPending : OwnershipOfferRevocation.NotFound;
    }
}
