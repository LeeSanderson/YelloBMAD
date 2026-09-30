using Yello.Application.Spaces;

namespace Yello.Infrastructure.Persistence;

/// <summary>
/// The application-state half of Space isolation: which Space this request's queries are scoped
/// to, held in memory and never read back from the database. AD-2 / AR-7.
/// </summary>
/// <remarks>
/// <para>
/// <b>This is what makes the EF Core query filters an INDEPENDENT second layer.</b> AD-2
/// (<c>ARCHITECTURE-SPINE.md:84</c>): the filters restate every policy "derived from application
/// state rather than session context, so neither layer alone carries NFR-1". A filter reading
/// <c>SESSION_CONTEXT()</c> would be the same layer twice. The filters close over
/// <see cref="SpaceId"/> through <see cref="YelloDbContext"/>, so if the session context were ever
/// set wrongly the filter would still exclude the other Space's rows, and the reverse.
/// </para>
/// <para>
/// <b>Request-scoped, and nothing here outlives the request.</b> NFR-2 forbids any cache that does,
/// and a Role held past the request that read it is exactly that cache. One instance per DI scope,
/// populated by <see cref="MembershipActiveSpaceResolver"/> and by nothing else - the mutators are
/// internal to this assembly for that reason.
/// </para>
/// <para>
/// <b>Fails closed.</b> Until a Space is named, <see cref="SpaceId"/> is <c>null</c>, and a filter
/// comparing a non-nullable column with <c>null</c> matches no row. A context created with no scope
/// - the design-time factory, a test context - therefore sees no Space-scoped row at all rather than
/// every one.
/// </para>
/// </remarks>
public sealed class SpaceScope
{
    /// <summary>
    /// The Space this request's Space-scoped queries are filtered to, or <c>null</c> for none.
    /// </summary>
    public Guid? SpaceId { get; private set; }

    /// <summary>
    /// The resolved context, or <c>null</c> until a Membership has been verified.
    /// </summary>
    public ActiveSpaceContext? Resolved { get; private set; }

    /// <summary>
    /// Narrows the filters to the requested Space while its Membership is being verified.
    /// </summary>
    /// <param name="spaceId">The requested Space.</param>
    internal void Narrow(Guid spaceId) => SpaceId = spaceId;

    /// <summary>
    /// Records the verified context.
    /// </summary>
    /// <param name="context">The context read from the Membership row.</param>
    internal void Resolve(ActiveSpaceContext context)
    {
        SpaceId = context.SpaceId;
        Resolved = context;
    }

    /// <summary>
    /// Returns to the fail-closed state after a refusal.
    /// </summary>
    internal void Clear()
    {
        SpaceId = null;
        Resolved = null;
    }
}
