using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Options;
using Yello.Application.Spaces;
using Yello.Contracts;

namespace Yello.Host.Spaces;

/// <summary>
/// How the pipeline's <c>Idempotency-Key</c> behaviour identifies a request and turns a response into a
/// record and back. Pure functions of the request and the response: no store, no decision. Story 2.1,
/// Task 7.
/// </summary>
/// <remarks>
/// Split out of <see cref="ActiveSpaceResolution"/> only to keep that class within S1200's limit; the
/// behaviour - when a key is read, replayed, refused or recorded - stays there.
/// </remarks>
internal static class IdempotentResponses
{
    private const string JsonContentType = "application/json; charset=utf-8";

    /// <summary>
    /// Whether a method changes state, and so honours an <c>Idempotency-Key</c>.
    /// </summary>
    /// <param name="method">The method.</param>
    /// <returns><c>true</c> for <c>POST</c>, <c>PUT</c>, <c>PATCH</c> and <c>DELETE</c>.</returns>
    private static bool IsStateChanging(string method) =>
        HttpMethods.IsPost(method) || HttpMethods.IsPut(method) || HttpMethods.IsPatch(method) || HttpMethods.IsDelete(method);

    /// <summary>
    /// Reads a state-changing request's <c>Idempotency-Key</c>.
    /// </summary>
    /// <param name="httpRequest">The request.</param>
    /// <param name="key">The key; <c>null</c> when the request carries none or is not state-changing.</param>
    /// <returns>
    /// <c>false</c> when the header is present and unusable - empty, repeated, or longer than
    /// <see cref="Idempotency.MaxKeyLength"/> characters.
    /// </returns>
    internal static bool TryReadKey(HttpRequest httpRequest, out string? key)
    {
        key = null;

        if (!IsStateChanging(httpRequest.Method)
            || !httpRequest.Headers.TryGetValue(Idempotency.HeaderName, out var values))
        {
            return true;
        }

        var sent = values.ToString();

        if (values.Count != 1 || string.IsNullOrWhiteSpace(sent) || sent.Length > Idempotency.MaxKeyLength)
        {
            return false;
        }

        key = sent;

        return true;
    }

    /// <summary>
    /// What identifies a request for its key: the method, the path, the query string and the SHA-256 of
    /// the body, hashed together - so the same key on another Project's rename is a different request.
    /// The body is buffered, so the endpoint can still read it.
    /// </summary>
    /// <param name="httpRequest">The request.</param>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    /// <returns>64 hexadecimal characters.</returns>
    internal static async Task<string> FingerprintAsync(HttpRequest httpRequest, CancellationToken cancellationToken)
    {
        httpRequest.EnableBuffering();

        var bodyHash = await SHA256.HashDataAsync(httpRequest.Body, cancellationToken).ConfigureAwait(false);

        httpRequest.Body.Position = 0;

        var identity = string.Join(
            '\n',
            httpRequest.Method.ToUpperInvariant(),
            httpRequest.Path.Value?.ToUpperInvariant() ?? string.Empty,
            httpRequest.QueryString.Value ?? string.Empty,
            Convert.ToHexString(bodyHash));

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(identity)));
    }

    /// <summary>
    /// Turns an endpoint's materialised success into the record to keep - without any secret the
    /// endpoint declares.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>What it can keep, and the rules that make that enough</b> (story 2.2, Task 8;
    /// <c>deferred-work.md:465-470</c>). The body is kept only from an <see cref="IValueHttpResult"/>, so a
    /// success is recorded faithfully only when the endpoint answers <c>Results.Json</c> or
    /// <c>Results.Ok</c>, and a result that writes any other body is refused here rather than replayed empty; a handler declared to return anything but <see cref="IResult"/> is refused by the
    /// architecture suite (<c>Every_Space_scoped_state_changing_endpoint_answers_a_result_the_record_can_replay</c>),
    /// which also closes "a non-<see cref="IResult"/> is a <c>500</c> only when keyed".
    /// </para>
    /// <para>
    /// <c>Location</c> is read from the response headers, where the endpoint sets it before returning.
    /// <c>Results.Created</c> and the <c>Accepted</c> factories write it only when the result executes -
    /// after this runs - so they are banned in the Host
    /// (<c>No_endpoint_answers_through_Results_Created_or_Accepted</c>).
    /// </para>
    /// </remarks>
    /// <param name="httpContext">The request, whose response headers carry any <c>Location</c>.</param>
    /// <param name="fingerprint">The request's fingerprint.</param>
    /// <param name="result">What the endpoint returned.</param>
    /// <returns>The record.</returns>
    /// <exception cref="InvalidOperationException">
    /// The result is not an <see cref="IResult"/>, or it writes a body that is not a value - text, bytes, a
    /// file or a stream - which a replay would answer empty.
    /// </exception>
    internal static RecordedResponse Capture(HttpContext httpContext, string fingerprint, object? result)
    {
        if (result is not IResult)
        {
            throw new InvalidOperationException(
                $"A Space-scoped endpoint answered an Idempotency-Key request with {result?.GetType().Name ?? "null"}, "
                + "which cannot be recorded for replay. Return an IResult.");
        }

        var status = (result as IStatusCodeHttpResult)?.StatusCode ?? StatusCodes.Status200OK;
        var location = httpContext.Response.Headers.Location.ToString();

        if (result is not IValueHttpResult { Value: { } value })
        {
            // A result that writes a body of its own - text, bytes, a file, a stream - would be replayed
            // empty, so it is refused rather than recorded wrongly (AC12). A bodiless one is kept as it is.
            if (result is IContentTypeHttpResult or IFileHttpResult)
            {
                throw new InvalidOperationException(
                    $"A Space-scoped endpoint answered an Idempotency-Key request with {result.GetType().Name}, "
                    + "whose body the record cannot replay. Answer with Results.Json or Results.Ok.");
            }

            return new RecordedResponse(fingerprint, status, ContentType: null, NullIfEmpty(location), string.Empty);
        }

        var options = httpContext.RequestServices.GetRequiredService<IOptions<Microsoft.AspNetCore.Http.Json.JsonOptions>>()
            .Value.SerializerOptions;

        var body = JsonSerializer.Serialize(value, value.GetType(), options);
        var stored = SecretPropertyOf(httpContext) is { } secret ? Without(body, secret, options) : body;

        return new RecordedResponse(fingerprint, status, JsonContentType, NullIfEmpty(location), stored);
    }

    /// <summary>
    /// The first answer to a key: the recorded body itself, so it and every replay are the same bytes -
    /// except where the endpoint declares a secret, which the first answer alone carries, as the
    /// endpoint wrote it.
    /// </summary>
    /// <param name="httpContext">The request.</param>
    /// <param name="record">What was recorded.</param>
    /// <param name="result">What the endpoint returned.</param>
    /// <returns>The answer.</returns>
    internal static object? FirstAnswer(HttpContext httpContext, RecordedResponse record, object? result) =>
        SecretPropertyOf(httpContext) is null && record.ContentType is { } contentType
            ? Results.Content(record.Body, contentType, Encoding.UTF8, record.StatusCode)
            : result;

    /// <summary>
    /// The recorded response, as it was answered the first time - less any secret, and then never
    /// cached.
    /// </summary>
    /// <param name="httpContext">The request.</param>
    /// <param name="recorded">The record.</param>
    /// <returns>The answer.</returns>
    internal static IResult Replay(HttpContext httpContext, RecordedResponse recorded)
    {
        if (recorded.Location is { } location)
        {
            httpContext.Response.Headers.Location = location;
        }

        if (SecretPropertyOf(httpContext) is not null)
        {
            httpContext.Response.Headers.CacheControl = "no-store";
        }

        return recorded.ContentType is { } contentType
            ? Results.Content(recorded.Body, contentType, Encoding.UTF8, recorded.StatusCode)
            : Results.StatusCode(recorded.StatusCode);
    }

    private static string? NullIfEmpty(string value) => string.IsNullOrEmpty(value) ? null : value;

    private static string? SecretPropertyOf(HttpContext httpContext) =>
        httpContext.GetEndpoint()?.Metadata.GetMetadata<ResponseSecret>()?.Property;

    /// <summary>
    /// The body with the secret property removed, so the record never holds it (NFR-6).
    /// </summary>
    /// <remarks>
    /// <b>Fails closed.</b> A declared name that the serialised body does not carry - a naming policy
    /// or a <c>[JsonPropertyName]</c> that drifted from the declaration - would otherwise store the
    /// secret as it stands. It throws instead, so the request fails and nothing is recorded.
    /// </remarks>
    private static string Without(string body, string property, JsonSerializerOptions options)
    {
        if (JsonNode.Parse(body) is not JsonObject json)
        {
            throw new InvalidOperationException("A response declared to carry a secret is not a JSON object.");
        }

        if (!json.Remove(property))
        {
            throw new InvalidOperationException(
                $"The response has no '{property}' to remove: the secret it declares cannot be kept out of its record.");
        }

        return json.ToJsonString(options);
    }
}
