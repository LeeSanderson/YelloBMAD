namespace Yello.Domain.Statuses;

/// <summary>
/// A Project's effective Status set, derived at the point of use and never stored. AD-16 / AR-23.
/// </summary>
/// <remarks>
/// <para>
/// <b>The one place the derivation lives.</b> Until story 6.1 adds a Project's own delta, the effective
/// set is its Space's definitions; 6.1 adds the delta here and every caller follows.
/// </para>
/// <para>
/// <b>Ordered by <c>Position</c>, stably.</b> Two definitions sharing a position keep the order they were
/// read in, which <c>IStatusDefinitionReader</c> fixes as <c>Position</c> then <c>Id</c> in the database's
/// own <c>uniqueidentifier</c> order - .NET orders a <see cref="Guid"/> differently, so re-sorting by
/// <c>Id</c> here would make the tie disagree with every SQL reader (<c>deferred-work.md:271</c>).
/// </para>
/// </remarks>
public static class EffectiveStatusSet
{
    /// <summary>
    /// The effective set of a Project whose Space defines <paramref name="spaceDefaults"/>.
    /// </summary>
    /// <param name="spaceDefaults">The Space's Status definitions, in the reader's order.</param>
    /// <returns>The effective set, in Board order.</returns>
    public static IReadOnlyList<StatusDefinition> Of(IEnumerable<StatusDefinition> spaceDefaults)
    {
        ArgumentNullException.ThrowIfNull(spaceDefaults);

        return [.. spaceDefaults.OrderBy(status => status.Position)];
    }

    /// <summary>
    /// Whether <paramref name="statusId"/> is one of an effective set - the one membership test every slice
    /// that sets a Status makes (AD-17: no Task holds a Status outside its Project's effective set).
    /// </summary>
    /// <param name="effective">An effective set, as <see cref="Of"/> derived it.</param>
    /// <param name="statusId">The Status a request asks for.</param>
    /// <returns><c>true</c> when the set holds it.</returns>
    public static bool Contains(IReadOnlyList<StatusDefinition> effective, Guid statusId)
    {
        ArgumentNullException.ThrowIfNull(effective);

        return effective.Any(status => status.Id == statusId);
    }
}
