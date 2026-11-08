using Microsoft.EntityFrameworkCore;
using Yello.Domain.Spaces;

namespace Yello.Infrastructure.Persistence;

/// <summary>
/// The Ownership Offer writes another store makes inside its own transaction: the lapse <i>by event</i>, when a Membership an
/// offer names stops existing. FR-8, AD-5, AD-27.
/// </summary>
/// <remarks>
/// <para>
/// <b>Written, never derived.</b> Every <c>Pending</c> offer naming the Membership is set <c>Lapsed</c> - actionable or
/// already past its expiry - so no <c>Pending</c> row ever names a Membership that is gone, and none holds the Space's one
/// pending place for it. No Role changes and nothing is sent.
/// </para>
/// <para>
/// <b>The caller holds the Space's Memberships lock and calls this before its guarded <c>DELETE</c></b>, so a refused
/// removal rolls the lapse back with the rest of its unit of work. Here, beside <see cref="OwnershipOfferStore"/>, so that
/// the two of them are every writer of an offer's state.
/// </para>
/// </remarks>
internal static class OwnershipOfferWrites
{
    private const string PendingState = nameof(OwnershipOfferState.Pending);

    private const string LapsedState = nameof(OwnershipOfferState.Lapsed);

    /// <summary>
    /// Lapses every pending offer of the Space that names the Membership.
    /// </summary>
    /// <param name="dbContext">The caller's unit of work.</param>
    /// <param name="spaceId">The Space.</param>
    /// <param name="membershipId">The departing Membership.</param>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    /// <returns>How many offers lapsed.</returns>
    internal static Task<int> LapseNamingAsync(YelloDbContext dbContext, Guid spaceId, Guid membershipId, CancellationToken cancellationToken) =>
        dbContext.Database.ExecuteSqlAsync(
            $"""
            UPDATE dbo.[OwnershipOffer] SET State = {LapsedState}
            WHERE SpaceId = {spaceId}
              AND RecipientMembershipId = {membershipId}
              AND State = {PendingState};
            """,
            cancellationToken);
}
