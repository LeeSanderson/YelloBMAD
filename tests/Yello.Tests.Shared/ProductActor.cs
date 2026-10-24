using System.Net;
using System.Net.Http.Json;
using System.Text.Encodings.Web;
using System.Text.Json;
using Yello.Contracts.Accounts;
using Yello.Contracts.ApiTokens;
using Yello.Contracts.Spaces;

namespace Yello.Tests.Shared;

/// <summary>
/// One Account, seeded through the product - registered, signed in, holding one Token for its
/// Personal Space - and able to send as either surface.
/// </summary>
/// <remarks>
/// <para>
/// <b>Seeding is through the product</b> (test-design B2's first shape, story 1.9 Task 3):
/// <c>POST /api/v1/accounts</c> provisions the Personal Space, <c>POST /api/v1/sessions</c> signs in,
/// <c>GET /api/v1/spaces</c> reads the Space id, and <c>POST /spaces/{id}/api-tokens</c> issues the
/// Token. Every row exists because the product wrote it, so every constraint holds by construction and
/// no row-level security exemption is needed. Only the Account id is read by SQL - <c>Account</c>
/// carries no policy and no endpoint returns it.
/// </para>
/// <para>
/// <b>The two surfaces never share a client state.</b> <see cref="IsolationSurface.Session"/> sends
/// through <see cref="Session"/> (cookies, and the anti-forgery header on a state-changing request);
/// <see cref="IsolationSurface.ApiToken"/> sends through a second <see cref="WireSession"/> that has
/// never held a cookie, with the bearer alone. A case never branches on the surface; this does.
/// </para>
/// </remarks>
public sealed class ProductActor
{
    private const string SessionCookieName = "yello_session";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly WireSession _script;

    private readonly List<string> _otherSpaceNames = [];

    private ProductActor(WireSession session, WireSession script)
    {
        Session = session;
        _script = script;
    }

    /// <summary>
    /// The Account's id.
    /// </summary>
    public Guid AccountId { get; private set; }

    /// <summary>
    /// The Personal Space's id.
    /// </summary>
    public Guid SpaceId { get; private set; }

    /// <summary>
    /// The Personal Space's name, as the product returned it.
    /// </summary>
    public string SpaceName { get; private set; } = string.Empty;

    /// <summary>
    /// The display name, unique per actor - and so a disclosure signal in any form.
    /// </summary>
    public string DisplayName { get; private set; } = string.Empty;

    /// <summary>
    /// The address registered.
    /// </summary>
    public string EmailAddress { get; private set; } = string.Empty;

    /// <summary>
    /// The password registered.
    /// </summary>
    public string Password { get; private set; } = string.Empty;

    /// <summary>
    /// The Token's id.
    /// </summary>
    public Guid TokenId { get; private set; }

    /// <summary>
    /// The Token's plaintext, as issued once.
    /// </summary>
    public string TokenPlaintext { get; private set; } = string.Empty;

    /// <summary>
    /// The Session cookie's value as sign-in set it.
    /// </summary>
    public string SessionCookieValue { get; private set; } = string.Empty;

    /// <summary>
    /// The browser-shaped client: the Session cookie and the anti-forgery token.
    /// </summary>
    public WireSession Session { get; }

    /// <summary>
    /// Every form in which this actor's secrets or Space name could appear in a body or a log: the
    /// password, the Token plaintext, the Session cookie value, the Space name raw and JSON-escaped.
    /// </summary>
    public IReadOnlyList<string> Secrets =>
    [
        Password,
        TokenPlaintext,
        SessionCookieValue,
        .. new[] { SpaceName }.Concat(_otherSpaceNames)
            .SelectMany(name => new[] { name, JsonEncodedText.Encode(name, JavaScriptEncoder.Default).ToString() }),
    ];

    /// <summary>
    /// Adds the name of a further Space this actor holds (one a case wrote by SQL) to
    /// <see cref="Secrets"/>, so the log check looks for it too.
    /// </summary>
    /// <param name="spaceName">The Space's name.</param>
    public void HoldsSpaceNamed(string spaceName)
    {
        ArgumentException.ThrowIfNullOrEmpty(spaceName);

        _otherSpaceNames.Add(spaceName);
    }

    /// <summary>
    /// Registers, signs in, reads the Personal Space and issues a Token - all through the product.
    /// </summary>
    /// <param name="host">The running Host.</param>
    /// <param name="database">The Host's database, for the Account id only.</param>
    /// <param name="label">A short label for the display name; a random suffix makes it unique.</param>
    /// <returns>The actor.</returns>
    public static Task<ProductActor> RegisterAsync(RunningHost host, WireDatabase database, string label)
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(database);

        return RegisterCoreAsync(host, database, label);
    }

    private static async Task<ProductActor> RegisterCoreAsync(RunningHost host, WireDatabase database, string label)
    {
        var cursor = await host.LogCursorAsync().ConfigureAwait(false);
        var suffix = Guid.NewGuid().ToString("N")[..12];
        var actor = new ProductActor(new WireSession(host.Client), new WireSession(host.Client))
        {
            DisplayName = $"{label}{suffix}",
            EmailAddress = $"{label}-{suffix}@isolation.example",
            Password = "Pw-" + Guid.NewGuid().ToString("N"),
        };

        await actor.Session.FetchAntiforgeryTokenAsync().ConfigureAwait(false);

        Expect(HttpStatusCode.NoContent, await actor.Session.RegisterAsync(new RegisterAccountRequest(actor.DisplayName, actor.EmailAddress, actor.Password)).ConfigureAwait(false), "register");
        Expect(HttpStatusCode.NoContent, await actor.Session.SignInAsync(actor.EmailAddress, actor.Password).ConfigureAwait(false), "sign in");

        actor.SessionCookieValue = actor.Session.Cookies.TryGetValue(SessionCookieName, out var cookie)
            ? cookie
            : throw new InvalidOperationException("Sign-in set no Session cookie.");

        // The anti-forgery token held was minted for the anonymous caller; the browser fetches a fresh
        // one after sign-in, and so does this.
        await actor.Session.FetchAntiforgeryTokenAsync().ConfigureAwait(false);

        var spaces = Expect(HttpStatusCode.OK, await actor.Session.GetAsync(SpaceRoutes.Directory).ConfigureAwait(false), "list Spaces");

        using (var document = JsonDocument.Parse(spaces.Body))
        {
            var personal = document.RootElement.EnumerateArray().Single();
            actor.SpaceId = personal.GetProperty("id").GetGuid();
            actor.SpaceName = personal.GetProperty("name").GetString() ?? string.Empty;
        }

        var issued = await actor.IssueTokenAsync(actor.SpaceId, "isolation").ConfigureAwait(false);
        actor.TokenId = issued.Id;
        actor.TokenPlaintext = issued.Plaintext;
        actor.AccountId = await database.AccountIdOfAsync(actor.EmailAddress).ConfigureAwait(false);

        // AC10 for the seeding itself: these requests carry the password, set the cookie and return the
        // plaintext, so they are where a leak would most likely be. Anchored on the Host's own lines.
        var slice = await host.LogSinceAsync(cursor).ConfigureAwait(false);

        if (!slice.Contains("Request finished", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The Host logged no request-finished line for the seeding requests, so its log cannot be checked.");
        }

        // Counted, never printed: a message that quoted the secret would be the leak.
        var leaked = actor.Secrets.Count(secret => secret.Length > 0 && slice.Contains(secret, StringComparison.Ordinal));

        return leaked == 0
            ? actor
            : throw new InvalidOperationException($"The Host logged {leaked} secret(s) while seeding an Account through the product (AC10, spine :274).");
    }

    /// <summary>
    /// Issues a Token for <paramref name="spaceId"/> through the Session, as the browser does.
    /// </summary>
    /// <param name="spaceId">The Space to bind it to.</param>
    /// <param name="name">The Token's name.</param>
    /// <returns>Its id and plaintext.</returns>
    public async Task<(Guid Id, string Plaintext)> IssueTokenAsync(Guid spaceId, string name)
    {
        var response = Expect(
            HttpStatusCode.Created,
            await Session.PostAsync(ApiTokenRoutes.IssueIn(spaceId), JsonContent.Create(new IssueApiTokenRequest(name), options: JsonOptions)).ConfigureAwait(false),
            "issue a Token");

        using var document = JsonDocument.Parse(response.Body);

        return (document.RootElement.GetProperty("id").GetGuid(), document.RootElement.GetProperty("token").GetString() ?? string.Empty);
    }

    /// <summary>
    /// Sends as this actor on <paramref name="surface"/>.
    /// </summary>
    /// <param name="surface">Which surface.</param>
    /// <param name="method">GET, POST or DELETE.</param>
    /// <param name="path">The route.</param>
    /// <returns>The answer.</returns>
    /// <remarks>
    /// A POST carries a valid Token-issue body (<c>{"name": ...}</c>), the only POST a Space-scoped
    /// route takes today, so a refusal is the boundary's and never the body's.
    /// </remarks>
    public Task<WireResponse> SendAsync(IsolationSurface surface, HttpMethod method, string path)
    {
        ArgumentNullException.ThrowIfNull(method);

        return (surface, method.Method) switch
        {
            (IsolationSurface.Session, "GET") => Session.GetAsync(path),
            (IsolationSurface.Session, "POST") => Session.PostAsync(path, ValidBody()),
            (IsolationSurface.Session, "DELETE") => Session.DeleteAsync(path),
            (IsolationSurface.ApiToken, "GET") => _script.GetWithTokenAsync(path, TokenPlaintext),
            (IsolationSurface.ApiToken, "POST") => _script.PostWithTokenAsync(path, ValidBody(), TokenPlaintext),
            (IsolationSurface.ApiToken, "DELETE") => _script.DeleteWithTokenAsync(path, TokenPlaintext),
            _ => throw new ArgumentOutOfRangeException(nameof(method), $"{surface} {method} is not a surface request this harness sends."),
        };
    }

    /// <summary>
    /// A GET as this actor on <paramref name="surface"/> through a client of its own, so it can run
    /// concurrently with others: nothing it receives touches <see cref="Session"/>'s held cookies.
    /// </summary>
    /// <param name="client">A client bound to the Host.</param>
    /// <param name="surface">Which surface.</param>
    /// <param name="path">The route.</param>
    /// <returns>The answer.</returns>
    public Task<WireResponse> GetDetachedAsync(HttpClient client, IsolationSurface surface, string path)
    {
        var detached = new WireSession(client);

        if (surface == IsolationSurface.ApiToken)
        {
            return detached.GetWithTokenAsync(path, TokenPlaintext);
        }

        foreach (var (name, value) in Session.Cookies)
        {
            detached.HoldCookie(name, value);
        }

        return detached.GetAsync(path);
    }

    /// <summary>
    /// The same request with no credential at all: no cookie, no bearer, no anti-forgery header.
    /// </summary>
    /// <param name="client">A client bound to the Host.</param>
    /// <param name="method">GET, POST or DELETE.</param>
    /// <param name="path">The route.</param>
    /// <returns>The answer.</returns>
    public static Task<WireResponse> SendWithoutCredentialAsync(HttpClient client, HttpMethod method, string path)
    {
        ArgumentNullException.ThrowIfNull(method);

        var anonymous = new WireSession(client);

        return method.Method switch
        {
            "GET" => anonymous.GetAnonymouslyAsync(path),
            "POST" => anonymous.PostAsync(path, ValidBody(), withAntiforgeryToken: false),
            "DELETE" => anonymous.DeleteAsync(path, withAntiforgeryToken: false, attachCookies: false),
            _ => throw new ArgumentOutOfRangeException(nameof(method), $"{method} is not a request this harness sends."),
        };
    }

    /// <summary>
    /// Signs this actor's Session out (<c>DELETE /api/v1/sessions</c>), then holds the pre-sign-out
    /// cookie again, so the next Session request is the replay Lee's decision 1 says grants nothing.
    /// </summary>
    /// <returns>The sign-out answer.</returns>
    public async Task<WireResponse> SignOutAndKeepTheOldCookieAsync()
    {
        var answer = await Session.DeleteAsync(SessionRoutes.Authenticate).ConfigureAwait(false);

        Session.HoldCookie(SessionCookieName, SessionCookieValue);

        return answer;
    }

    /// <summary>
    /// <c>DELETE /api/v1/sessions</c> with the bearer Token alone and no anti-forgery header.
    /// </summary>
    /// <returns>The answer.</returns>
    public Task<WireResponse> SignOutWithTokenAsync() =>
        _script.DeleteWithTokenAsync(SessionRoutes.Authenticate, TokenPlaintext);

    private static JsonContent ValidBody() =>
        JsonContent.Create(new IssueApiTokenRequest("from an isolation case"), options: JsonOptions);

    private static WireResponse Expect(HttpStatusCode expected, WireResponse response, string step) =>
        response.StatusCode == expected
            ? response
            : throw new InvalidOperationException($"Seeding through the product failed to {step}: {response.StatusCode} {response.Body}");
}
