namespace Yello.Application.Spaces;

/// <summary>
/// Keeps the responses to <c>Idempotency-Key</c> requests made without a Space - Space creation - so a retry is
/// answered with the original response and creates nothing. AD-18 / AR-25; story 3.1, open question 2 (a).
/// </summary>
/// <remarks>
/// <para>
/// <b>Why it exists.</b> A lost response to <c>POST /api/v1/spaces</c> would otherwise create a second Space - the
/// exact duplicate AD-18 exists to stop - and the client re-sends an unanswered create with its original key.
/// </para>
/// <para>
/// <b>Scoped to (Account, key)</b>, under the <c>AccountId</c> session key: the table has its own row-level security
/// policy on that key and a query filter on the request's Account, so another Account's key is never seen. The
/// request pipeline is its only caller (AR-3, A-12).
/// </para>
/// </remarks>
public interface IAccountIdempotencyStore
{
    /// <summary>
    /// How long a record answers a retry - the same 24 hours a Space-scoped record does (story 2.1), stated here so
    /// this port depends on no other.
    /// </summary>
    static TimeSpan Retention { get; } = TimeSpan.FromHours(24);

    /// <summary>
    /// The response recorded for a key by the acting Account.
    /// </summary>
    /// <param name="accountId">The acting Account.</param>
    /// <param name="key">The key, as sent.</param>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    /// <returns>The recorded response, or <c>null</c> when the key is unused or its record has expired.</returns>
    Task<RecordedResponse?> FindAsync(Guid accountId, string key, CancellationToken cancellationToken);

    /// <summary>
    /// Records the response to a key's first use, in the same transaction as its effect.
    /// </summary>
    /// <param name="accountId">The acting Account.</param>
    /// <param name="key">The key, as sent.</param>
    /// <param name="response">The response to replay.</param>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    /// <returns><c>true</c> when recorded; <c>false</c> when a concurrent first use committed first.</returns>
    Task<bool> TryRecordAsync(Guid accountId, string key, RecordedResponse response, CancellationToken cancellationToken);
}
