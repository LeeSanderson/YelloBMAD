namespace Yello.Contracts;

/// <summary>
/// The <c>Idempotency-Key</c> contract, stated once for the client that sends it and the Host that
/// honours it. AD-18 / AR-25.
/// </summary>
/// <remarks>
/// <para>
/// <b>Optional, and honoured by the request pipeline on every Space-scoped state-changing request.</b>
/// A request carrying a key the same Account already used in the same Space, with the same request,
/// is answered with the original response and applies nothing; with a different request it is refused
/// <see cref="ProblemTypes.IdempotencyKeyReused"/>. A request with no key is served as usual.
/// </para>
/// <para>
/// <b>A client sends a fresh key for each attempt a person starts</b>, and the same key when it
/// retries that attempt.
/// </para>
/// </remarks>
public static class Idempotency
{
    private const int LongestKey = 255;

    /// <summary>
    /// The request header.
    /// </summary>
    public static string HeaderName => "Idempotency-Key";

    /// <summary>
    /// The longest key the Host accepts, in characters. A longer one is refused
    /// <see cref="ProblemTypes.InvalidIdempotencyKey"/>.
    /// </summary>
    public static int MaxKeyLength => LongestKey;
}
