namespace Yello.Application.Invitations;

/// <summary>
/// Opens the one unit of work the Invitation view and the accept run on: the token resolved to its Space on the server,
/// and the <c>SpaceId</c> session key set to that Space alone. Story 4.3; Lee's Q1 (a) and Q2 (a); AD-2 and AD-24 as
/// amended on 2026-11-02, AD-28.
/// </summary>
/// <remarks>
/// <para>
/// <b>The key comes from the server, never from the request</b> (AD-2). The token only identifies the offer: its hash is
/// looked up in the policy-free token index, and the Space id stored there at issue is the one the key is set to. A
/// token that resolves to nothing - never issued, malformed, or cascaded away with its Space - opens the same unit under a
/// server-generated sentinel id, so every state does the same work and the read that follows sees nothing (Lee's Q6
/// (a)).
/// </para>
/// <para>
/// <b>The <c>SpaceId</c> key alone, never <c>AccountId</c></b>: the caller holds no Membership in the Space yet, so no
/// <c>ActiveSpaceContext</c> is resolved and no Role is read. It is not an Account-scoped surface, and the one unit of work
/// that sets both keys is still Space creation's.
/// </para>
/// <para>
/// <b>The request pipeline in <c>Yello.Host</c> is its only caller</b> (AR-3): a slice never opens it, and a gate holds
/// that nothing but the pipeline's Invitation step reaches it.
/// </para>
/// </remarks>
public interface IInvitationScope
{
    /// <summary>
    /// Resolves <paramref name="presentedToken"/> through the token index and opens the unit of work, keyed to the Space
    /// the index names (or a sentinel).
    /// </summary>
    /// <param name="presentedToken">The token as presented: possibly empty, malformed or never issued.</param>
    /// <param name="accountId">The Session's Account, or <c>null</c> without a Session. Never a key: carried for the slice.</param>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    /// <returns>The open unit of work.</returns>
    Task<IInvitationUnitOfWork> BeginAsync(string presentedToken, Guid? accountId, CancellationToken cancellationToken);
}

/// <summary>
/// One Invitation view's or accept's unit of work, which owns its connection from open to close. Disposing it without
/// <see cref="CompleteAsync"/> rolls everything back - which is all the view ever does.
/// </summary>
public interface IInvitationUnitOfWork : IAsyncDisposable
{
    /// <summary>
    /// What the unit runs under.
    /// </summary>
    InvitationContext Context { get; }

    /// <summary>
    /// Commits the unit of work.
    /// </summary>
    /// <param name="cancellationToken">Cancels the commit; the pipeline passes <see cref="CancellationToken.None"/>.</param>
    /// <returns>A task that completes once the work is durable.</returns>
    Task CompleteAsync(CancellationToken cancellationToken);
}

/// <summary>
/// What an Invitation view or accept runs under: the Space and the Invitation the token resolved to on the server (or
/// sentinels for a token that resolved to nothing), and the Session's Account. Built by the pipeline, never by a slice and
/// never from the request (AD-2).
/// </summary>
/// <param name="SpaceId">The Space the session key names.</param>
/// <param name="InvitationId">The Invitation the token named, or a sentinel that names none.</param>
/// <param name="AccountId">The Session's Account, or <c>null</c> without one.</param>
public sealed record InvitationContext(Guid SpaceId, Guid InvitationId, Guid? AccountId);
