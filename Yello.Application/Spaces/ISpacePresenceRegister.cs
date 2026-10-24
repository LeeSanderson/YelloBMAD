namespace Yello.Application.Spaces;

/// <summary>
/// Admits an Account to a Space's live presence, under NFR-8's concurrent-active-Sessions bound.
/// AD-25 / AR-30.
/// </summary>
/// <remarks>
/// <para>
/// <b>Called by the request pipeline, inside the resolved unit of work</b>, after the Membership is
/// confirmed and under the Space's session context. The count and the write share that transaction,
/// so a refusal is raised "inside the same transaction as the creation it refuses" (AC7) and leaves
/// nothing written. The pipeline is the only caller (AR-3).
/// </para>
/// <para>
/// <b>The limit is passed in, not known here.</b> The pipeline reads it from
/// <c>BoundRegistry</c>, the one place NFR-8's values are declared.
/// </para>
/// </remarks>
public interface ISpacePresenceRegister
{
    /// <summary>
    /// Records the Account as present in the Space, unless doing so would exceed the limit.
    /// </summary>
    /// <param name="accountId">The acting Account.</param>
    /// <param name="spaceId">The resolved Space.</param>
    /// <param name="limit">The most Accounts that may be present at once.</param>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    /// <returns>
    /// <c>true</c> when the Account is present - already, or newly admitted; <c>false</c> when the
    /// Space is at the limit and the Account was not already counted in it.
    /// </returns>
    Task<bool> TryEnterAsync(Guid accountId, Guid spaceId, int limit, CancellationToken cancellationToken);
}
