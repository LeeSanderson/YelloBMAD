namespace Yello.Contracts;

/// <summary>
/// The part of an RFC 9457 <c>application/problem+json</c> body Yello's client actually reads.
/// </summary>
/// <remarks>
/// <para>
/// <b>Deliberately partial.</b> A problem body also carries <c>title</c>, <c>detail</c>,
/// <c>status</c> and <c>instance</c>; none of them is here, because none of them is the contract.
/// AR-34 makes the <c>type</c> the stable machine-readable part and says prose is never the
/// contract - so a client that deserialised <c>title</c> would be one refactor away from
/// rendering server English into a localised interface.
/// </para>
/// <para>
/// <b>It exists because <c>ProblemDetails</c> does not reach the browser.</b> ASP.NET Core's own
/// type lives in <c>Microsoft.AspNetCore.Mvc</c>, which <c>Yello.Contracts</c> is banned from
/// referencing by the per-ring package rule and which would drag server surface into the
/// WebAssembly payload.
/// </para>
/// </remarks>
/// <param name="Type">
/// The stable identifier, matched against <see cref="ProblemTypes"/>. Null when the response was
/// not a problem body Yello produced.
/// </param>
/// <param name="Errors">
/// The failed rules, by their stable codes. The client uses each code as a resource key, so the
/// server never sends wording and a new locale needs no server change.
/// </param>
public sealed record ProblemResponse(string? Type, IReadOnlyList<string>? Errors);
