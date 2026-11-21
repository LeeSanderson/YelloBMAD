using System.Text.Json;
using Yello.Contracts.Accounts;

namespace Yello.Tests.Shared;

/// <summary>
/// Deleting one's own Account over the wire: <c>DELETE /api/v1/account</c>, Session only, no body and no
/// <c>Idempotency-Key</c>. <c>204</c> clears the Session cookie; an Account that still owns a Space is refused
/// <c>409 account-owns-spaces</c> with a <c>spaces</c> extension listing each owned Space as <c>{ id, name }</c>.
/// </summary>
/// <remarks>
/// <b>The old cookie is kept on purpose</b> by <see cref="DeleteAndKeepTheOldCookieAsync"/>: the <c>204</c> clears the
/// cookie in the session, and a case about what a dead Account's cookie gets must send that cookie again.
/// </remarks>
public static class AccountDeletionWire
{
    private const string SessionCookieName = "yello_session";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private static readonly Dictionary<string, string> NoHeaders = new(StringComparer.Ordinal);

    /// <summary>
    /// The members of each item in the refusal's <c>spaces</c> list, in ordinal order: identity only.
    /// </summary>
    public static IReadOnlyList<string> OwnedSpaceMembers { get; } = ["id", "name"];

    /// <summary>
    /// The deletion as the browser sends it: the Session's cookie and anti-forgery header, no body, no key.
    /// </summary>
    /// <param name="session">The caller's Session.</param>
    /// <returns>The answer.</returns>
    public static Task<WireResponse> DeleteAsync(WireSession session)
    {
        ArgumentNullException.ThrowIfNull(session);

        return session.SendWithHeadersAsync(HttpMethod.Delete, AccountRoutes.Account, content: null, NoHeaders);
    }

    /// <summary>
    /// The deletion, then the Session cookie held before it is held again.
    /// </summary>
    /// <param name="session">The caller's Session.</param>
    /// <returns>The deletion's answer.</returns>
    public static Task<WireResponse> DeleteAndKeepTheOldCookieAsync(WireSession session)
    {
        ArgumentNullException.ThrowIfNull(session);

        return DeleteAndKeepTheOldCookieCoreAsync(session);
    }

    private static async Task<WireResponse> DeleteAndKeepTheOldCookieCoreAsync(WireSession session)
    {
        var cookie = session.Cookies.TryGetValue(SessionCookieName, out var value)
            ? value
            : throw new InvalidOperationException("The session holds no Session cookie to keep.");

        var answer = await DeleteAsync(session).ConfigureAwait(false);

        session.HoldCookie(SessionCookieName, cookie);

        return answer;
    }

    /// <summary>
    /// The owned Spaces a refusal lists, in the order sent, each with the member names it carries.
    /// </summary>
    /// <param name="body">The refusal's body.</param>
    /// <returns>The listed Spaces; empty when the body has no <c>spaces</c> member.</returns>
    public static IReadOnlyList<OwnedSpace> OwnedSpacesIn(string body)
    {
        using var document = JsonDocument.Parse(body);

        if (!document.RootElement.TryGetProperty("spaces", out var spaces) || spaces.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return [.. spaces.EnumerateArray().Select(space => new OwnedSpace(
            space.GetProperty("id").GetGuid(),
            space.GetProperty("name").GetString() ?? string.Empty,
            [.. space.EnumerateObject().Select(member => member.Name).Order(StringComparer.Ordinal)]))];
    }

    /// <summary>
    /// The refusal's <c>type</c>, or <c>null</c> when the body is not a problem.
    /// </summary>
    /// <param name="body">The body.</param>
    /// <returns>The type.</returns>
    public static string? ProblemTypeOf(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return null;
        }

        var problem = JsonSerializer.Deserialize<JsonElement>(body, JsonOptions);

        return problem.ValueKind == JsonValueKind.Object && problem.TryGetProperty("type", out var type) ? type.GetString() : null;
    }
}

/// <summary>
/// One owned Space as the refusal lists it.
/// </summary>
/// <param name="Id">The Space.</param>
/// <param name="Name">Its name.</param>
/// <param name="Members">The JSON member names the item carried, in ordinal order.</param>
public sealed record OwnedSpace(Guid Id, string Name, IReadOnlyList<string> Members);
