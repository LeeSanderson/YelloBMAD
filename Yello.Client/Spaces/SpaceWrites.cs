using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Yello.Client.Sessions;
using Yello.Contracts;

namespace Yello.Client.Spaces;

/// <summary>
/// Sends a state-changing request about the active Space: its <c>Idempotency-Key</c>, an anti-forgery
/// token, and one resend - with the same key - when the held token is refused. Stories 2.1 and 2.2.
/// </summary>
/// <remarks>
/// <para>
/// <b>One copy, shared by every Space-scoped write</b> - the Project list's create and rename, and the
/// Board's Task creation - so the key and token rules cannot drift between them.
/// </para>
/// <para>
/// <b>The caller owns the key</b>: a fresh one per attempt, and the same one again for a resend after an
/// attempt that got no answer, because that attempt may have been applied (2.1 review, R3).
/// </para>
/// </remarks>
/// <param name="http">The API client.</param>
/// <param name="antiforgery">The anti-forgery token source.</param>
public sealed class SpaceWrites(HttpClient http, AntiforgeryTokenSource antiforgery)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// Sends the request, and once more with the same key if the anti-forgery token is refused.
    /// </summary>
    /// <param name="method">The method.</param>
    /// <param name="path">The path.</param>
    /// <param name="body">The body, serialised as JSON.</param>
    /// <param name="key">The <c>Idempotency-Key</c>.</param>
    /// <param name="token">The active Space's token, cancelled by its purge.</param>
    /// <returns>The answer; the caller disposes it.</returns>
    public Task<SpaceWriteAnswer> SendAsync(HttpMethod method, string path, object body, string key, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(body);

        return SendCoreAsync(method, path, body, key, token);
    }

    /// <summary>
    /// The problem body of a refusal, or <c>null</c> when it carries none.
    /// </summary>
    /// <param name="response">The response.</param>
    /// <param name="token">The cancellation token.</param>
    /// <returns>The problem, or <c>null</c>.</returns>
    public static Task<ProblemResponse?> ProblemOfAsync(HttpResponseMessage response, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(response);

        return ProblemOfCoreAsync(response, token);
    }

    private static async Task<ProblemResponse?> ProblemOfCoreAsync(HttpResponseMessage response, CancellationToken token) =>
        response.Content.Headers.ContentType?.MediaType is "application/problem+json"
            ? await response.Content.ReadFromJsonAsync<ProblemResponse>(JsonOptions, token)
            : null;

    private async Task<SpaceWriteAnswer> SendCoreAsync(HttpMethod method, string path, object body, string key, CancellationToken token)
    {
        await antiforgery.EnsureTokenAsync();

        var answer = await AnswerOfAsync(await http.SendAsync(Message(method, path, body, key), token), token);

        if (answer.StatusCode != HttpStatusCode.BadRequest || answer.Problem?.Type != ProblemTypes.AntiforgeryTokenMissing)
        {
            return answer;
        }

        answer.Dispose();
        antiforgery.Invalidate();
        await antiforgery.EnsureTokenAsync();

        return await AnswerOfAsync(await http.SendAsync(Message(method, path, body, key), token), token);
    }


    private static HttpRequestMessage Message(HttpMethod method, string path, object body, string key)
    {
        var message = new HttpRequestMessage(method, new Uri(path, UriKind.Relative))
        {
            Content = JsonContent.Create(body, body.GetType(), options: JsonOptions),
        };

        message.Headers.TryAddWithoutValidation(Idempotency.HeaderName, key);

        return message;
    }

    private static async Task<SpaceWriteAnswer> AnswerOfAsync(HttpResponseMessage response, CancellationToken token)
    {
        if (response.IsSuccessStatusCode)
        {
            return new SpaceWriteAnswer(response, Problem: null);
        }

        try
        {
            return new SpaceWriteAnswer(response, await ProblemOfCoreAsync(response, token));
        }
        catch
        {
            response.Dispose();
            throw;
        }
    }
}

/// <summary>
/// A state-changing request's response, and its problem body when it was refused with one.
/// </summary>
/// <param name="Response">The response.</param>
/// <param name="Problem">The problem body, or <c>null</c>.</param>
public sealed record SpaceWriteAnswer(HttpResponseMessage Response, ProblemResponse? Problem) : IDisposable
{
    /// <summary>
    /// The response's status.
    /// </summary>
    public HttpStatusCode StatusCode => Response.StatusCode;

    /// <inheritdoc />
    public void Dispose() => Response.Dispose();
}
