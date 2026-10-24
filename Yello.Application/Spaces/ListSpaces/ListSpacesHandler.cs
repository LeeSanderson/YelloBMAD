namespace Yello.Application.Spaces.ListSpaces;

/// <summary>
/// Lists the Spaces the acting Account belongs to - the Space switcher's read. FR-9; AD-24 / AR-29.
/// </summary>
/// <remarks>
/// <para>
/// <b>Not a refusal surface.</b> An Account holding no Membership is answered with an empty list
/// (story 1.7, open question 6): belonging to no Space is a valid state, and nothing about it is a
/// boundary crossing to record.
/// </para>
/// <para>
/// <b>Nothing cross-cutting is re-implemented here</b> (AR-3). The pipeline established the
/// Account-scoped context from the Session before this ran; this slice never reads a principal, a
/// Role or a Space.
/// </para>
/// </remarks>
/// <param name="directory">Reads the Account's Spaces under its own Account-scoped unit of work.</param>
public sealed class ListSpacesHandler(IAccountSpaceDirectory directory)
{
    /// <summary>
    /// Lists the Account's Spaces.
    /// </summary>
    /// <param name="context">The Account-scoped context the pipeline established.</param>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    /// <returns>The Spaces, as identity only, ordered by name then id.</returns>
    public Task<IReadOnlyList<SpaceIdentity>> HandleAsync(
        AccountScopedContext context,
        CancellationToken cancellationToken) =>
        directory.ListAsync(context, cancellationToken);
}
