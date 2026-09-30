using Yello.Domain.Accounts;
using Yello.Domain.Memberships;
using Yello.Domain.Spaces;
using Yello.Domain.Statuses;
using Yello.Infrastructure.Persistence;
using Yello.Tests.Slices.Accounts.RegisterAccount;

namespace Yello.Tests.Slices.Spaces.ResolveActiveSpace;

/// <summary>
/// Blocker B2's answer, as code: how a test populates more than one Space when every Space-scoped
/// table is behind row-level security. AC8.
/// </summary>
/// <remarks>
/// <para>
/// <b>The shape chosen is the first of the three B2 names</b> (<c>test-design-progress.md:249-253</c>):
/// seed through the application, one Space per block, with the session context switched between
/// blocks. Each <see cref="SeedAsync"/> call is its own unit of work: a fresh context whose query
/// filters are narrowed to that one Space, a transaction, and the product's own
/// <c>SpaceSessionContext</c> setting the key - so the FILTER and the BLOCK predicates both see a
/// context that names the Space being written, exactly as a request's would.
/// </para>
/// <para>
/// <b>Why not the other two.</b> An RLS-exempt seeding principal is a hole if it ever reaches
/// production, and nothing about this suite needs one. Seeding before the policies exist would
/// mean running a partial migration chain and then migrating the rest, which every fixture in this
/// repository deliberately does not do: the schema under test is the one the full migration chain
/// produces. <b>This shape needs no principal, no permission and no configuration that production
/// lacks, so there is nothing to keep out of production.</b>
/// </para>
/// <para>
/// <b>A seed that succeeds is itself evidence.</b> The BLOCK predicate refuses any row whose
/// <c>SpaceId</c> the session context does not name, so a block that writes a row into a Space other
/// than the one it was opened for fails here rather than seeding silently.
/// </para>
/// <para>
/// <b>Story 1.9 inherits this.</b> Its isolation fixtures need two Spaces populated and a request made
/// as an Account holding Membership in only one; this is the helper that makes that setup.
/// </para>
/// </remarks>
internal static class SpaceSeeding
{
    /// <summary>
    /// Writes one block of rows as a unit of work scoped to one Space.
    /// </summary>
    /// <param name="database">The migrated database.</param>
    /// <param name="spaceId">The Space the block is scoped to.</param>
    /// <param name="add">Adds the block's entities to the context.</param>
    /// <param name="cancellationToken">The test's cancellation token.</param>
    /// <returns>A task that completes once the block is committed.</returns>
    public static async Task SeedAsync(
        RegistrationDatabase database,
        Guid spaceId,
        Action<YelloDbContext> add,
        CancellationToken cancellationToken)
    {
        var scope = new SpaceScope();
        scope.Narrow(spaceId);

        await using var context = database.CreateContext(scope);
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

        await SpaceSessionContext.SetAsync(context.Database, spaceId, cancellationToken);

        add(context);

        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    /// <summary>
    /// A new Account with a random address and no password. <c>Account</c> carries no policy, so it
    /// can be added in any block.
    /// </summary>
    /// <returns>The Account.</returns>
    public static Account NewAccount()
    {
        var address = $"seed-{Guid.NewGuid():N}@yello.test";

        return new Account
        {
            Id = Guid.NewGuid(),
            EmailAddress = address,
            NormalizedEmailAddress = EmailAddressNormalisation.Normalise(address),
            DisplayName = "Seeded",
            PasswordHash = null,
            CreatedAt = DateTimeOffset.UtcNow,
        };
    }

    /// <summary>
    /// A new Space. It carries no policy, but a Space-scoped row's foreign key needs it to exist, so
    /// it is added in the block that first writes into it.
    /// </summary>
    /// <param name="spaceId">The Space's id.</param>
    /// <returns>The Space.</returns>
    public static Space NewSpace(Guid spaceId) =>
        new() { Id = spaceId, Name = "Seeded " + spaceId.ToString("N")[..8], CreatedAt = DateTimeOffset.UtcNow };

    /// <summary>
    /// A Membership of an Account in a Space at a Role.
    /// </summary>
    /// <param name="spaceId">The Space.</param>
    /// <param name="accountId">The Account.</param>
    /// <param name="role">The Role.</param>
    /// <returns>The Membership.</returns>
    public static Membership NewMembership(Guid spaceId, Guid accountId, Role role) =>
        new()
        {
            Id = Guid.NewGuid(),
            SpaceId = spaceId,
            AccountId = accountId,
            Role = role,
            CreatedAt = DateTimeOffset.UtcNow,
        };

    /// <summary>
    /// A Status definition in a Space.
    /// </summary>
    /// <param name="spaceId">The Space.</param>
    /// <param name="name">Its name - distinct per Space in these tests, so an assertion is on identity.</param>
    /// <returns>The Status definition.</returns>
    public static StatusDefinition NewStatus(Guid spaceId, string name) =>
        new()
        {
            Id = Guid.NewGuid(),
            SpaceId = spaceId,
            Name = name,
            Position = 0,
            CreatedAt = DateTimeOffset.UtcNow,
        };
}
