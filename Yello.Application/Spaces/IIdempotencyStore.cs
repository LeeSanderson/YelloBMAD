namespace Yello.Application.Spaces;

/// <summary>
/// Keeps the responses to <c>Idempotency-Key</c> requests in the active Space, so a retry is answered
/// with the original response and applies nothing. AD-18 / AR-25; story 2.1, Task 7.
/// </summary>
/// <remarks>
/// <para>
/// <b>The request pipeline is its only caller</b> (AR-3, A-12). A slice never reads or writes a key.
/// </para>
/// <para>
/// <b>Scoped to (Space, Account, key)</b>, inside the request's Space-scoped unit of work: the record
/// holds Space content - a Project's name, in a stored body - so it is under the Space's policy like
/// every Space-scoped row, and another Account's key, or the same key in another Space, is never seen.
/// </para>
/// <para>
/// <b>Kept for <see cref="Retention"/>, evaluated on read.</b> An older record is treated as absent, and
/// expired records are deleted only inside their own Space's unit of work, when a new one is written:
/// the table is under the Space policy, so a purge across Spaces would need the bypass AD-2 forbids.
/// </para>
/// </remarks>
public interface IIdempotencyStore
{
    /// <summary>
    /// How long a record answers a retry.
    /// </summary>
    static TimeSpan Retention { get; } = TimeSpan.FromHours(24);

    /// <summary>
    /// The response recorded for a key, by the acting Account in the active Space.
    /// </summary>
    /// <param name="accountId">The acting Account.</param>
    /// <param name="key">The key, as sent.</param>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    /// <returns>The recorded response, or <c>null</c> when the key is unused or its record has expired.</returns>
    Task<RecordedResponse?> FindAsync(Guid accountId, string key, CancellationToken cancellationToken);

    /// <summary>
    /// Records the response to a key's first use, in the same transaction as its effect.
    /// </summary>
    /// <param name="spaceId">The active Space.</param>
    /// <param name="accountId">The acting Account.</param>
    /// <param name="key">The key, as sent.</param>
    /// <param name="response">The response to replay.</param>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    /// <returns>
    /// <c>true</c> when recorded; <c>false</c> when a concurrent first use of the same key committed
    /// first, in which case this request must be rolled back.
    /// </returns>
    Task<bool> TryRecordAsync(
        Guid spaceId,
        Guid accountId,
        string key,
        RecordedResponse response,
        CancellationToken cancellationToken);
}

/// <summary>
/// A response, as recorded for replay.
/// </summary>
/// <param name="Fingerprint">What identifies the request it answered: method, path and the body's hash.</param>
/// <param name="StatusCode">Its status.</param>
/// <param name="ContentType">Its content type, or <c>null</c> when it had no body.</param>
/// <param name="Location">Its <c>Location</c>, or <c>null</c>.</param>
/// <param name="Body">Its body, with any secret removed; empty when it had none.</param>
public sealed record RecordedResponse(string Fingerprint, int StatusCode, string? ContentType, string? Location, string Body);
