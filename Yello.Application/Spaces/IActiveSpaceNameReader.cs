namespace Yello.Application.Spaces;

/// <summary>
/// Reads the name of the Space the current unit of work was resolved to - for the context bar, and
/// for nothing else. Story 1.7, AC5 / AC10.
/// </summary>
/// <remarks>
/// <para>
/// <b>It takes no Space id, deliberately.</b> <c>Space</c> carries no row-level security policy
/// (Lee's decision at story 1.5, pending the AD-24 amendment), so a reader that accepted an id could
/// name any Space in the database. This one reads the Space the pipeline has already verified the
/// caller's Membership in on this request, and throws when no Space has been resolved - a wiring
/// defect, never a default.
/// </para>
/// <para>
/// <b>The name crosses no boundary.</b> It is the name of the Space the caller just proved
/// Membership in. Logs carry the Space's id, never this (AR-34).
/// </para>
/// </remarks>
public interface IActiveSpaceNameReader
{
    /// <summary>
    /// Reads the resolved Space's name.
    /// </summary>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    /// <returns>The name.</returns>
    Task<string> ReadAsync(CancellationToken cancellationToken);
}
