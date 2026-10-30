using Yello.Domain.Memberships;
using Yello.Domain.Statuses;

namespace Yello.Domain.Spaces;

/// <summary>
/// The rows a new Space is born with: the Space, its one Owner Membership and FR-24's default Statuses, all
/// stamped with one instant. Registration (AD-22) and Space creation (FR-5, story 3.1) both provision through
/// here, so a created Space and a provisioned one cannot drift apart.
/// </summary>
/// <remarks>
/// <para>
/// <b>Nothing distinguishes the two routes</b> (story 1.3, AC4): there is no flag, no parameter naming the caller
/// and no branch. The name is the only input that differs.
/// </para>
/// <para>
/// <b>Ownership is a Membership at Role Owner</b>, never an <c>OwnerId</c> on <see cref="Space"/>
/// (<c>addendum.md:33</c>). <b>Statuses are identity-bearing</b> (AR-23), positioned by their index in
/// <see cref="DefaultStatusSet.Names"/>, the one place their order is stated.
/// </para>
/// <para>
/// <b>Ids are drawn in a fixed order</b> - the Membership's, then each Status's - after the caller has drawn the
/// Space's, so registration's id sequence is what it was before this was extracted.
/// </para>
/// </remarks>
public static class NewSpace
{
    /// <summary>
    /// Builds the rows of a new Space.
    /// </summary>
    /// <param name="spaceId">The Space's id, server-generated before anything is written (AD-2).</param>
    /// <param name="accountId">The Account that becomes its Owner.</param>
    /// <param name="name">The Space's name, already normalised.</param>
    /// <param name="createdAt">The one instant every row carries.</param>
    /// <param name="identifiers">Draws the Membership's and the Statuses' ids.</param>
    /// <returns>The Space, its Owner Membership and its default Statuses.</returns>
    public static ProvisionedSpace Provision(
        Guid spaceId,
        Guid accountId,
        string name,
        DateTimeOffset createdAt,
        IIdentifierGenerator identifiers)
    {
        ArgumentNullException.ThrowIfNull(identifiers);

        var space = new Space
        {
            Id = spaceId,
            Name = name,
            CreatedAt = createdAt,
        };

        var ownerMembership = new Membership
        {
            Id = identifiers.Next(),
            SpaceId = spaceId,
            AccountId = accountId,
            Role = Role.Owner,
            CreatedAt = createdAt,
        };

        var statuses = DefaultStatusSet.Names
            .Select((statusName, index) => new StatusDefinition
            {
                Id = identifiers.Next(),
                SpaceId = spaceId,
                Name = statusName,
                Position = index,
                CreatedAt = createdAt,
            })
            .ToList();

        return new ProvisionedSpace(space, ownerMembership, statuses);
    }
}

/// <summary>
/// The rows one new Space is born with.
/// </summary>
/// <param name="Space">The Space - an ordinary Space in every respect.</param>
/// <param name="OwnerMembership">The single Membership at Role Owner.</param>
/// <param name="Statuses">The Space's default Status set.</param>
public sealed record ProvisionedSpace(
    Space Space,
    Membership OwnerMembership,
    IReadOnlyList<StatusDefinition> Statuses);
