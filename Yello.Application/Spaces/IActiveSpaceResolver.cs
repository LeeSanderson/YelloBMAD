namespace Yello.Application.Spaces;

/// <summary>
/// Opens a Space-scoped unit of work: resolves the <see cref="ActiveSpaceContext"/> from the
/// Membership row and scopes the connection to that Space before anything else runs. AD-2 / AR-6.
/// </summary>
/// <remarks>
/// <para>
/// <b>Declared here and implemented in <c>Yello.Infrastructure</c></b>, because the work is a
/// transaction and a session-context call that this ring may not see. The request pipeline in
/// <c>Yello.Host</c> is its only caller - AR-3 makes Space resolution a pipeline concern, and a
/// slice that calls this itself is re-implementing it.
/// </para>
/// <para>
/// <b>Set, then verify.</b> The Membership read that establishes the context is itself behind the
/// row-level security policy it unlocks, so the implementation scopes the connection to the
/// requested Space <i>first</i> and then reads the Account's Membership under that scope. The read
/// can only ever see Memberships in that one Space: a row there means the Account is a member, and
/// no row means the request is refused. Recorded as story 1.5's decision on the bootstrap problem
/// in its Completion Notes.
/// </para>
/// </remarks>
public interface IActiveSpaceResolver
{
    /// <summary>
    /// Scopes a new unit of work to a Space and resolves the Account's standing in it.
    /// </summary>
    /// <param name="accountId">The acting Account, however the request established it.</param>
    /// <param name="spaceId">The requested Space, however the request established it.</param>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    /// <returns>
    /// The open unit of work, or <c>null</c> when the Account holds no Membership in that Space -
    /// which includes a Space that does not exist. <b>One <c>null</c> for both</b>, so no caller
    /// can distinguish them however carefully it reads the result (AD-3's "never distinguished").
    /// </returns>
    Task<IActiveSpaceUnitOfWork?> BeginAsync(
        Guid accountId,
        Guid spaceId,
        CancellationToken cancellationToken);
}

/// <summary>
/// One request's Space-scoped unit of work, which owns its connection from open to close.
/// </summary>
/// <remarks>
/// Disposing without <see cref="CompleteAsync"/> rolls everything back. That is the path a failed
/// or cancelled request takes, and it leaves nothing half-written.
/// </remarks>
public interface IActiveSpaceUnitOfWork : IAsyncDisposable
{
    /// <summary>
    /// The resolved context. Every authorisation decision on this request reads it and nothing
    /// else.
    /// </summary>
    ActiveSpaceContext Context { get; }

    /// <summary>
    /// Commits the unit of work.
    /// </summary>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    /// <returns>A task that completes once the work is durable.</returns>
    Task CompleteAsync(CancellationToken cancellationToken);
}
