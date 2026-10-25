namespace Yello.Host.Spaces;

/// <summary>
/// Endpoint metadata: the response carries a secret, in the named JSON property, that must never be
/// stored or shown twice. NFR-6; story 2.1, Task 7 (<c>deferred-work.md:372</c>).
/// </summary>
/// <remarks>
/// <b>Read by the pipeline's idempotency record.</b> The first response is sent as the endpoint wrote it;
/// the record keeps the body with this property removed, and a replay answers that body with
/// <c>Cache-Control: no-store</c>. So a datastore read never yields the secret, and a retry never shows
/// it a second time.
/// </remarks>
/// <param name="Property">The JSON property name, as serialised.</param>
public sealed record ResponseSecret(string Property);

/// <summary>
/// Declares an endpoint's <see cref="ResponseSecret"/>.
/// </summary>
public static class ResponseSecretExtensions
{
    /// <summary>
    /// Declares that the endpoint's response carries a secret in <paramref name="property"/>.
    /// </summary>
    /// <param name="builder">The endpoint.</param>
    /// <param name="property">The JSON property name, as serialised.</param>
    /// <returns>The same endpoint, for chaining.</returns>
    public static RouteHandlerBuilder CarriesSecretInResponse(this RouteHandlerBuilder builder, string property) =>
        builder.WithMetadata(new ResponseSecret(property));
}
