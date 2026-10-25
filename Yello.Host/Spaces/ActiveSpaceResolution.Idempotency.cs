using Yello.Application.Spaces;

namespace Yello.Host.Spaces;

/// <summary>
/// <c>Idempotency-Key</c>, as the request pipeline honours it on every Space-scoped state-changing
/// request. AD-18 / AR-25, A-12; story 2.1, Task 7.
/// </summary>
/// <remarks>
/// <para>
/// <b>Part of <see cref="ActiveSpaceResolution"/>, deliberately</b>: AR-3 makes idempotency a pipeline
/// behaviour, and the A-12 gate holds that only the pipeline depends on the idempotency port. It runs
/// after resolution, the capability check and the presence bound, before the creation bound, inside
/// the request's unit of work.
/// </para>
/// <para>
/// <b>With no header it does nothing.</b> With one, it looks the key up for (Space, Account): unused,
/// the endpoint runs and a materialised success is recorded in the same transaction as its effect; used
/// with the same request, the recorded response is answered and the endpoint never runs; used with a
/// different request, <c>422</c>. A concurrent first use that loses the unique index is <c>409</c>,
/// rolled back. A failure is never recorded - the unit of work rolls it back anyway.
/// </para>
/// <para>
/// How a request is identified and a response recorded is <see cref="IdempotentResponses"/>.
/// </para>
/// </remarks>
internal static partial class ActiveSpaceResolution
{
    /// <summary>
    /// Reads the request's key and answers it from its record when it has one.
    /// </summary>
    /// <returns>
    /// No key, the key and fingerprint to record once the endpoint has answered, or the answer itself - a
    /// replay or a refusal.
    /// </returns>
    private static async Task<IdempotentRequest> BeginIdempotentAsync(HttpContext httpContext, BoundaryRequest request)
    {
        if (!IdempotentResponses.TryReadKey(httpContext.Request, out var key))
        {
            SpaceLog.IdempotencyKeyRefused(request.Logger, request.SpaceId, "malformed");

            return IdempotentRequest.Answered(BoundaryProblems.InvalidIdempotencyKey());
        }

        if (key is null)
        {
            return IdempotentRequest.None;
        }

        var fingerprint = await IdempotentResponses.FingerprintAsync(httpContext.Request, request.CancellationToken)
            .ConfigureAwait(false);

        var recorded = await request.Services.GetRequiredService<IIdempotencyStore>()
            .FindAsync(request.AccountId, key, request.CancellationToken)
            .ConfigureAwait(false);

        if (recorded is null)
        {
            return new IdempotentRequest(key, fingerprint, Answer: null);
        }

        if (!string.Equals(recorded.Fingerprint, fingerprint, StringComparison.Ordinal))
        {
            SpaceLog.IdempotencyKeyRefused(request.Logger, request.SpaceId, "reused");

            return IdempotentRequest.Answered(BoundaryProblems.IdempotencyKeyReused());
        }

        SpaceLog.IdempotentReplay(request.Logger, request.SpaceId);

        return IdempotentRequest.Answered(IdempotentResponses.Replay(httpContext, recorded));
    }

    /// <summary>
    /// Records a materialised success for the request's key, in the same unit of work as its effect.
    /// </summary>
    /// <returns>The answer, or <c>409</c> when a concurrent first use of the key committed first.</returns>
    private static async Task<object?> RecordIdempotentAsync(
        HttpContext httpContext,
        BoundaryRequest request,
        IdempotentRequest idempotent,
        object? result)
    {
        var record = IdempotentResponses.Capture(httpContext, idempotent.Fingerprint!, result);

        var recorded = await request.Services.GetRequiredService<IIdempotencyStore>()
            .TryRecordAsync(request.SpaceId, request.AccountId, idempotent.Key!, record, request.CancellationToken)
            .ConfigureAwait(false);

        if (recorded)
        {
            return IdempotentResponses.FirstAnswer(httpContext, record, result);
        }

        SpaceLog.IdempotencyKeyRefused(request.Logger, request.SpaceId, "in flight");

        // A Location the endpoint set describes work that is about to be rolled back.
        httpContext.Response.Headers.Remove("Location");

        return BoundaryProblems.IdempotencyKeyInFlight();
    }

    /// <summary>
    /// The request's <c>Idempotency-Key</c>, as the pipeline found it.
    /// </summary>
    /// <param name="Key">The key to record against, or <c>null</c>.</param>
    /// <param name="Fingerprint">The request's fingerprint, with a key.</param>
    /// <param name="Answer">The answer when the key decided it - a replay or a refusal.</param>
    private sealed record IdempotentRequest(string? Key, string? Fingerprint, IResult? Answer)
    {
        internal static IdempotentRequest None { get; } = new(null, null, null);

        internal static IdempotentRequest Answered(IResult answer) => new(null, null, answer);
    }
}
