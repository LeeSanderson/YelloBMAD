using System.Net.Http.Json;
using System.Text.Json;
using Yello.Contracts.Accounts;

namespace Yello.Tests.Slices;

/// <summary>
/// Cookies and anti-forgery tokens, handled explicitly, because both are what these tests are
/// about.
/// </summary>
/// <remarks>
/// <para>
/// <b>At the suite root rather than beside one use case, because both endpoints need it.</b>
/// AR-14 requires an anti-forgery token on <i>every</i> state-changing request, so story 1.4's
/// decision reached <c>POST /api/v1/accounts</c> as well - and story 1.3's endpoint tests, which
/// had never sent one, began failing the moment it did. That is the gate working: a shipped
/// endpoint's contract changed and its tests said so.
/// </para>
/// <para>
/// <b><c>HostProcess</c> turns the framework's cookie container off, so every cookie that travels
/// in these tests travels because a line here put it there.</b> That is the property that makes the
/// assertions mean something: a container would attach the Session cookie invisibly, and a test
/// asserting a request was authenticated could pass without anyone having said which cookie carried
/// it. The Session cookie's attributes ARE AC5's contract, so they are read off the header rather
/// than inferred from behaviour.
/// </para>
/// <para>
/// <b>The Host under test listens on <c>https</c>.</b> This paragraph previously ended "- which is
/// what the Host under test listens on", of <c>http</c>; that was the diagnosis that caused
/// <c>HostProcess</c> to be moved to <c>https</c> in this same story, so it described a state that
/// no longer existed by the time it was written. Corrected at code review on 2026-09-19. What
/// remains true is the reason above: reading the header is how the attributes get asserted at all,
/// and a <c>CookieContainer</c> would attach cookies invisibly.
/// </para>
/// </remarks>
internal sealed class WireSession(HttpClient client)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly Dictionary<string, string> _cookies = new(StringComparer.Ordinal);

    /// <summary>
    /// The anti-forgery request token, once <see cref="FetchAntiforgeryTokenAsync"/> has run.
    /// </summary>
    public string? AntiforgeryRequestToken { get; private set; }

    /// <summary>
    /// The raw <c>Set-Cookie</c> values the last response carried, exactly as sent.
    /// </summary>
    public IReadOnlyList<string> LastSetCookies { get; private set; } = [];

    /// <summary>
    /// Every cookie currently held, by name.
    /// </summary>
    public IReadOnlyDictionary<string, string> Cookies => _cookies;

    /// <summary>
    /// Fetches the anti-forgery request token and keeps the cookie half that pairs with it.
    /// </summary>
    public async Task FetchAntiforgeryTokenAsync()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(SessionRoutes.AntiforgeryToken, UriKind.Relative));

        using var response = await SendAsync(request).ConfigureAwait(false);

        var token = await response.Content
            .ReadFromJsonAsync<AntiforgeryTokenResponse>(JsonOptions)
            .ConfigureAwait(false);

        AntiforgeryRequestToken = token?.RequestToken;
    }

    /// <summary>
    /// Posts a sign-in submission, carrying whatever cookies and token are held.
    /// </summary>
    /// <param name="emailAddress">The address.</param>
    /// <param name="password">The password.</param>
    /// <param name="withAntiforgeryToken">
    /// Whether to send the token header. <c>false</c> is the R8 negative case - a state-changing
    /// request without one must be refused.
    /// </param>
    /// <param name="origin">An <c>Origin</c> header to send, for the CORS cases.</param>
    /// <returns>The response, with its body already read.</returns>
    public async Task<WireResponse> SignInAsync(
        string emailAddress,
        string password,
        bool withAntiforgeryToken = true,
        string? origin = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(SessionRoutes.Authenticate, UriKind.Relative))
        {
            Content = JsonContent.Create(new SignInRequest(emailAddress, password), options: JsonOptions),
        };

        if (withAntiforgeryToken && AntiforgeryRequestToken is { } token)
        {
            request.Headers.TryAddWithoutValidation(Antiforgery.HeaderName, token);
        }

        if (origin is not null)
        {
            request.Headers.TryAddWithoutValidation("Origin", origin);
        }

        using var response = await SendAsync(request).ConfigureAwait(false);

        return await WireResponse.ReadAsync(response).ConfigureAwait(false);
    }

    /// <summary>
    /// Posts a registration, carrying whatever cookies and token are held.
    /// </summary>
    /// <param name="request">The submission.</param>
    /// <param name="withAntiforgeryToken">
    /// Whether to send the token header. <c>false</c> is the negative case - AR-14 reaches
    /// registration too, so a submission without one must be refused there as well.
    /// </param>
    /// <returns>The response, with its body already read.</returns>
    public Task<WireResponse> RegisterAsync(
        RegisterAccountRequest request,
        bool withAntiforgeryToken = true) =>
        PostAsync(
            AccountRoutes.Register,
            JsonContent.Create(request, options: JsonOptions),
            withAntiforgeryToken);

    /// <summary>
    /// Posts arbitrary content to a route, for the cases that send something a DTO cannot express.
    /// </summary>
    /// <param name="route">The route.</param>
    /// <param name="content">The body.</param>
    /// <param name="withAntiforgeryToken">Whether to send the token header.</param>
    /// <returns>The response, with its body already read.</returns>
    public async Task<WireResponse> PostAsync(
        string route,
        HttpContent content,
        bool withAntiforgeryToken = true)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(route, UriKind.Relative))
        {
            Content = content,
        };

        if (withAntiforgeryToken && AntiforgeryRequestToken is { } token)
        {
            request.Headers.TryAddWithoutValidation(Antiforgery.HeaderName, token);
        }

        using var response = await SendAsync(request).ConfigureAwait(false);

        return await WireResponse.ReadAsync(response).ConfigureAwait(false);
    }

    /// <summary>
    /// Sends a <c>DELETE</c> to a route, carrying whatever cookies and token are held. Added for
    /// story 1.7's sign-out (<c>DELETE /api/v1/sessions</c>), the first state-changing verb other
    /// than <c>POST</c>.
    /// </summary>
    /// <param name="route">The route.</param>
    /// <param name="withAntiforgeryToken">Whether to send the token header.</param>
    /// <param name="attachCookies">
    /// Whether to send the held cookies. <c>false</c> is the "no Session at all" case.
    /// </param>
    /// <returns>The response, with its body already read.</returns>
    public async Task<WireResponse> DeleteAsync(
        string route,
        bool withAntiforgeryToken = true,
        bool attachCookies = true)
    {
        using var request = new HttpRequestMessage(HttpMethod.Delete, new Uri(route, UriKind.Relative));

        if (withAntiforgeryToken && AntiforgeryRequestToken is { } token)
        {
            request.Headers.TryAddWithoutValidation(Antiforgery.HeaderName, token);
        }

        using var response = await SendAsync(request, attachCookies).ConfigureAwait(false);

        return await WireResponse.ReadAsync(response).ConfigureAwait(false);
    }

    /// <summary>
    /// Asks whether the Session held is still good.
    /// </summary>
    /// <param name="sessionCookie">
    /// The cookie to send, or <c>null</c> to send whatever is held. Passed explicitly by the cases
    /// that tamper with it.
    /// </param>
    /// <returns>The response, with its body already read.</returns>
    public async Task<WireResponse> CurrentSessionAsync(string? sessionCookie = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(SessionRoutes.Current, UriKind.Relative));

        if (sessionCookie is not null)
        {
            request.Headers.TryAddWithoutValidation("Cookie", sessionCookie);
        }

        using var response = await SendAsync(request, attachCookies: sessionCookie is null).ConfigureAwait(false);

        return await WireResponse.ReadAsync(response).ConfigureAwait(false);
    }

    /// <summary>
    /// Gets a route, carrying whatever cookies are held.
    /// </summary>
    /// <param name="route">The route.</param>
    /// <returns>The response, with its body already read.</returns>
    public async Task<WireResponse> GetAsync(string route)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(route, UriKind.Relative));

        using var response = await SendAsync(request).ConfigureAwait(false);

        return await WireResponse.ReadAsync(response).ConfigureAwait(false);
    }

    /// <summary>
    /// Gets a route carrying no cookie at all, whatever is held.
    /// </summary>
    /// <param name="route">The route.</param>
    /// <returns>The response, with its body already read.</returns>
    public async Task<WireResponse> GetAnonymouslyAsync(string route)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(route, UriKind.Relative));

        using var response = await SendAsync(request, attachCookies: false).ConfigureAwait(false);

        return await WireResponse.ReadAsync(response).ConfigureAwait(false);
    }

    /// <summary>
    /// Sends a CORS preflight for a sign-in from a given origin.
    /// </summary>
    /// <param name="origin">The origin to claim.</param>
    /// <returns>The response.</returns>
    public async Task<WireResponse> PreflightSignInAsync(string origin)
    {
        using var request = new HttpRequestMessage(HttpMethod.Options, new Uri(SessionRoutes.Authenticate, UriKind.Relative));

        request.Headers.TryAddWithoutValidation("Origin", origin);
        request.Headers.TryAddWithoutValidation("Access-Control-Request-Method", "POST");
        request.Headers.TryAddWithoutValidation(
            "Access-Control-Request-Headers",
            $"content-type,{Antiforgery.HeaderName}");

        using var response = await SendAsync(request).ConfigureAwait(false);

        return await WireResponse.ReadAsync(response).ConfigureAwait(false);
    }

    /// <summary>
    /// The <c>Set-Cookie</c> value for a named cookie from the last response, or <c>null</c>.
    /// </summary>
    /// <param name="name">The cookie's name.</param>
    /// <returns>The whole directive, attributes included.</returns>
    public string? SetCookieFor(string name) =>
        LastSetCookies.FirstOrDefault(
            value => value.StartsWith(name + "=", StringComparison.Ordinal));

    private async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, bool attachCookies = true)
    {
        if (attachCookies && _cookies.Count > 0)
        {
            request.Headers.TryAddWithoutValidation(
                "Cookie",
                string.Join("; ", _cookies.Select(pair => $"{pair.Key}={pair.Value}")));
        }

        var response = await client.SendAsync(request).ConfigureAwait(false);

        LastSetCookies = response.Headers.TryGetValues("Set-Cookie", out var values)
            ? [.. values]
            : [];

        foreach (var value in LastSetCookies)
        {
            var pair = value.Split(';', 2)[0];
            var separator = pair.IndexOf('=', StringComparison.Ordinal);

            if (separator > 0)
            {
                _cookies[pair[..separator]] = pair[(separator + 1)..];
            }
        }

        return response;
    }
}

/// <summary>
/// A response, read into memory so two of them can be compared to each other after both have been
/// disposed.
/// </summary>
/// <remarks>
/// <b>Comparing two responses to each other is the shape AD-23 actually requires</b>, and it is the
/// shape story 1.3's <c>A_duplicate_registration_answers_exactly_as_a_new_one_does</c> established.
/// Asserting that both are 401 would still pass if one grew a header, a body or a different content
/// type.
/// </remarks>
/// <param name="StatusCode">The status.</param>
/// <param name="Body">The whole body, as text.</param>
/// <param name="ContentType">The content type, or <c>null</c>.</param>
/// <param name="HeaderNames">Every header name, ordered, less the ones that differ between any two responses.</param>
/// <param name="Headers">Every header, by name, for the cases that read one.</param>
internal sealed record WireResponse(
    System.Net.HttpStatusCode StatusCode,
    string Body,
    string? ContentType,
    IReadOnlyList<string> HeaderNames,
    IReadOnlyDictionary<string, string[]> Headers)
{
    /// <summary>
    /// Reads a response into a comparable value.
    /// </summary>
    /// <param name="response">The response.</param>
    /// <returns>The value.</returns>
    public static async Task<WireResponse> ReadAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

        var headers = response.Headers
            .Concat(response.Content.Headers)
            .ToDictionary(header => header.Key, header => header.Value.ToArray(), StringComparer.OrdinalIgnoreCase);

        // Date differs between any two responses and says nothing about either.
        var names = headers.Keys
            .Where(name => !name.Equals("Date", StringComparison.OrdinalIgnoreCase))
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new WireResponse(
            response.StatusCode,
            body,
            response.Content.Headers.ContentType?.ToString(),
            names,
            headers);
    }

    /// <summary>
    /// The first value of a header, or <c>null</c>.
    /// </summary>
    /// <param name="name">The header name.</param>
    /// <returns>The value.</returns>
    public string? Header(string name) =>
        Headers.TryGetValue(name, out var values) ? values.FirstOrDefault() : null;
}
