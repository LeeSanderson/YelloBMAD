using Yello.Contracts;
using Yello.Domain;
using Yello.Domain.Bounds;
using Yello.Domain.Memberships;

namespace Yello.Host.Spaces;

/// <summary>
/// The RFC 9457 bodies the Space boundary answers with. AR-34; AD-3.
/// </summary>
/// <remarks>
/// <para>
/// <b>The <c>type</c> is the contract; the titles are not.</b> The client never reads a title, so each
/// is a fixed, neutral string - and fixed matters as much as neutral: two causes answered with the
/// same <c>type</c> and the same title produce byte-identical bodies, which is what AC1 asserts.
/// </para>
/// <para>
/// <b>Nothing object-shaped, ever.</b> No resource id, no title of anything, no Space name. A
/// <c>403</c> names the capability refused by its stable code; a bound refusal names the bound and
/// its number.
/// </para>
/// </remarks>
internal static class BoundaryProblems
{
    /// <summary>
    /// The neutral title every <c>404</c> carries, whatever its cause.
    /// </summary>
    internal const string NotAvailableTitle = "Not available.";

    /// <summary>
    /// The one answer for every not-found the product gives - a Space-boundary refusal, and an
    /// <c>/api</c> path that matches no route. Byte-identical whatever the cause.
    /// </summary>
    /// <returns>A <c>404</c> problem response.</returns>
    internal static IResult NotAvailable() =>
        Results.Problem(
            statusCode: StatusCodes.Status404NotFound,
            type: ProblemTypes.NotAvailable,
            title: NotAvailableTitle);

    /// <summary>
    /// The authentication boundary's answer, the same one <c>GET /api/v1/sessions/current</c> gives.
    /// </summary>
    /// <returns>A <c>401</c> problem response.</returns>
    internal static IResult Unauthenticated() =>
        Results.Problem(
            statusCode: StatusCodes.Status401Unauthorized,
            type: ProblemTypes.AuthenticationFailed,
            title: "Authentication did not succeed.");

    /// <summary>
    /// The <c>403</c>: capability-shaped, carrying the capability's stable code and nothing that
    /// names an object or a Space.
    /// </summary>
    /// <param name="capability">The capability refused.</param>
    /// <returns>A <c>403</c> problem response.</returns>
    internal static IResult InsufficientRole(Capability capability) =>
        Results.Problem(
            statusCode: StatusCodes.Status403Forbidden,
            type: ProblemTypes.InsufficientRole,
            title: "The Role held in this Space does not permit this.",
            extensions: new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["capability"] = StableCode.Of(capability),
            });

    /// <summary>
    /// An <c>Idempotency-Key</c> header that is empty, repeated, or over the length limit. Story 2.1.
    /// </summary>
    /// <returns>A <c>400</c> problem response.</returns>
    internal static IResult InvalidIdempotencyKey() =>
        Results.Problem(
            statusCode: StatusCodes.Status400BadRequest,
            type: ProblemTypes.InvalidIdempotencyKey,
            title: "An Idempotency-Key is 1 to 255 characters.");

    /// <summary>
    /// An <c>Idempotency-Key</c> already used by this Account in this Space for a different request.
    /// </summary>
    /// <returns>A <c>422</c> problem response.</returns>
    internal static IResult IdempotencyKeyReused() =>
        Results.Problem(
            statusCode: StatusCodes.Status422UnprocessableEntity,
            type: ProblemTypes.IdempotencyKeyReused,
            title: "This Idempotency-Key was used for a different request.");

    /// <summary>
    /// An <c>Idempotency-Key</c> whose first use committed while this request was running.
    /// </summary>
    /// <returns>A <c>409</c> problem response.</returns>
    internal static IResult IdempotencyKeyInFlight() =>
        Results.Problem(
            statusCode: StatusCodes.Status409Conflict,
            type: ProblemTypes.IdempotencyKeyInFlight,
            title: "A request with this Idempotency-Key was still in progress.");

    /// <summary>
    /// The bound refusal: <c>409</c>, with the bound's stable code and its value, so the client can
    /// state the number plainly.
    /// </summary>
    /// <param name="bound">The bound reached.</param>
    /// <returns>A <c>409</c> problem response.</returns>
    internal static IResult BoundReached(BoundEntry bound) =>
        Results.Problem(
            statusCode: StatusCodes.Status409Conflict,
            type: ProblemTypes.BoundReached,
            title: "A limit was reached.",
            extensions: new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["bound"] = StableCode.Of(bound.Bound),
                ["limit"] = bound.Limit,
            });
}
