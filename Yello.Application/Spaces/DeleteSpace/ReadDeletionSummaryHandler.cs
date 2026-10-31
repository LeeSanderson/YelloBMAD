namespace Yello.Application.Spaces.DeleteSpace;

/// <summary>
/// Reads what deleting the active Space would take, for the confirm to state: its Project and Task counts and how many
/// other Accounts would lose access. Story 3.3, AC2, open question 2 (a); UX-DR37, PRD §6.2.
/// </summary>
/// <remarks>
/// <b>Owner-only, by the pipeline</b>: the route declares <c>DeleteSpace</c>, so the counts are read by exactly the Role that
/// may act on them. Counts and nothing else - no name crosses this answer.
/// </remarks>
/// <param name="contents">Counts the Space's contents.</param>
public sealed class ReadDeletionSummaryHandler(ISpaceContentsReader contents)
{
    /// <summary>
    /// Counts the resolved Space's contents.
    /// </summary>
    /// <param name="context">The context the pipeline resolved.</param>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    /// <returns>The counts.</returns>
    public Task<SpaceContents> HandleAsync(ActiveSpaceContext context, CancellationToken cancellationToken) =>
        contents.CountAsync(context, cancellationToken);
}
