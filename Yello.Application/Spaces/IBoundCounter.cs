using Yello.Domain.Bounds;

namespace Yello.Application.Spaces;

/// <summary>
/// Counts what a creation bound limits, in the active Space, inside the request's unit of work.
/// AD-25 / AR-30; stories 2.1 and 2.2.
/// </summary>
/// <remarks>
/// <para>
/// <b>The request pipeline is its only caller</b> (AR-3): a bound is declared once, in
/// <see cref="BoundRegistry"/>, and checked by the pipeline before the endpoint runs. A slice that
/// counted would be the place a bound is checked twice, or once and wrongly.
/// </para>
/// <para>
/// <b>Some bounds count within something narrower than the Space</b> - Tasks per Project counts one
/// Project's Tasks. The pipeline reads that scope from the route, never the slice, and a bound that
/// needs a scope fails closed without one.
/// </para>
/// <para>
/// <b>The count holds its lock until the unit of work ends.</b> Two creations at one below the limit
/// must not both pass: the second waits for the first to commit and then counts the first's row.
/// </para>
/// </remarks>
public interface IBoundCounter
{
    /// <summary>
    /// Counts what <paramref name="bound"/> limits in the active Space, within
    /// <paramref name="scopeId"/> when the bound is scoped, and holds that count until the unit of work
    /// ends.
    /// </summary>
    /// <param name="bound">The bound.</param>
    /// <param name="scopeId">
    /// What the count is scoped to within the Space - the Project, for Tasks per Project - or
    /// <c>null</c> for a bound counted across the Space.
    /// </param>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    /// <returns>The current count.</returns>
    /// <exception cref="ArgumentException">A scoped bound was asked for without its scope.</exception>
    Task<int> CountAsync(Bound bound, Guid? scopeId, CancellationToken cancellationToken);
}
