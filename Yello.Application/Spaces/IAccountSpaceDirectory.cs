namespace Yello.Application.Spaces;

/// <summary>
/// Lists the Spaces an Account holds a Membership in - the one sanctioned read across Spaces.
/// AD-24 / AR-29, FR-9.
/// </summary>
/// <remarks>
/// <para>
/// <b>Declared here and implemented in <c>Yello.Infrastructure</c></b>, because the work is a
/// transaction and an <c>AccountId</c> session-context call this ring may not see. The
/// implementation opens its own Account-scoped unit of work, sets the <c>AccountId</c> key and no
/// other, and reads through the <c>Membership</c> table's widened policy and filter - so which
/// Spaces it can name is decided by both isolation layers rather than by a <c>WHERE</c> clause here.
/// </para>
/// <para>
/// <b>Ordered by name, then by id.</b> Space names are not unique - they are derived from display
/// names, which are not (<c>sprint-change-proposal-2026-09-13.md:175</c>) - so the id breaks the tie
/// and the order is deterministic.
/// </para>
/// </remarks>
public interface IAccountSpaceDirectory
{
    /// <summary>
    /// Lists the Account's Spaces.
    /// </summary>
    /// <param name="context">The Account-scoped context the pipeline established.</param>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    /// <returns>Every Space the Account holds a Membership in, as identity only. Empty is an answer.</returns>
    Task<IReadOnlyList<SpaceIdentity>> ListAsync(AccountScopedContext context, CancellationToken cancellationToken);
}
