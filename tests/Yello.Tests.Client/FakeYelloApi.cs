using System.Buffers.Text;
using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Playwright;
using Yello.Contracts;
using Yello.Contracts.ApiTokens;
using Yello.Contracts.Accounts;
using Yello.Contracts.Spaces;
using Yello.Tests.Shared;

namespace Yello.Tests.Client;

/// <summary>
/// The Host's API, faked at the browser's network edge with <c>page.RouteAsync</c>, so a browser case
/// can drive a signed-in flow with no Host, no database and no container. Story 1.7, open question 5.
/// </summary>
/// <remarks>
/// <para>
/// <b>Written by the story 1.7 ATDD run on 2026-10-24.</b> Story 1.7 recommends this shape (Q5 option
/// a) because the machine has no memory for another SQL Server engine and a real Host inside this
/// suite would be a ring change. What it does <b>not</b> prove, stated so nobody reads more into a
/// green case than is there: the real Session cookie crossing origins, the real CORS preflight the
/// Host answers, and anything the Host decides. Those are re-owned by story 1.9.
/// </para>
/// <para>
/// <b>The answers are the Host's shapes.</b> Every body is a <c>Yello.Contracts</c> type
/// (<see cref="AntiforgeryTokenResponse"/>, <see cref="ProblemResponse"/>,
/// <see cref="SpaceIdentityResponse"/>, <see cref="ActiveSpaceResponse"/>) and every path is a
/// contract route, so a contract change breaks the fake as well.
/// </para>
/// <para>
/// <b>Every request is recorded</b>, so a case can assert that a request was never made - AC10's
/// "without issuing any Space-scoped request for it".
/// </para>
/// </remarks>
internal sealed class FakeYelloApi
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly ConcurrentQueue<FakeRequest> _requests = new();
    private readonly ConcurrentDictionary<Guid, TaskCompletionSource> _held = new();
    private readonly ConcurrentDictionary<Guid, TaskCompletionSource> _arrived = new();
    private readonly ConcurrentDictionary<Guid, TaskCompletionSource> _heldProjects = new();
    private readonly ConcurrentDictionary<Guid, TaskCompletionSource> _arrivedProjects = new();
    private readonly TaskCompletionSource<FakeRequest> _signInSeen = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource<FakeRequest> _signOutSeen = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>
    /// The anti-forgery request token this fake issues, unique per instance.
    /// </summary>
    public string IssuedToken { get; } = "fake-antiforgery-" + Guid.NewGuid().ToString("N");

    /// <summary>
    /// Whether <c>GET /api/v1/sessions/current</c> answers 204. A successful sign-in sets it; a
    /// sign-out clears it.
    /// </summary>
    public bool SignedIn { get; set; }

    /// <summary>
    /// Whether the first <c>DELETE /api/v1/sessions</c> is refused for its anti-forgery token, as
    /// the Host refuses a token minted to a Session that has since been ended on another device.
    /// </summary>
    public bool RefuseFirstSignOut { get; set; }

    /// <summary>
    /// The switcher's answer, in the order the Host serves it.
    /// </summary>
    public IList<(Guid Id, string Name)> Switcher { get; } = new List<(Guid Id, string Name)>();

    /// <summary>
    /// The Role, as its stable code, the Account holds in each Space it may be served.
    /// </summary>
    public IDictionary<Guid, string> Roles { get; } = new Dictionary<Guid, string>();

    /// <summary>
    /// The Account's API Tokens as the Account-settings list serves them. Story 1.8.
    /// </summary>
    public IList<FakeApiToken> ApiTokens { get; } = new List<FakeApiToken>();

    /// <summary>
    /// The plaintext the next issuance answers with - Q9's format, unique per instance, so a case can
    /// look for exactly this string in the DOM and in storage. Story 1.8.
    /// </summary>
    public string IssuedSecret { get; } = "yello_pat_" + Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(32));

    /// <summary>
    /// Each Space's Projects, as the list route serves them. Story 2.1; the routes are
    /// <see cref="ProjectRouteScaffold"/>'s until <c>Yello.Contracts.Projects</c> exists, and the bodies
    /// are <see cref="FakeProject"/>, shaped as story 2.1 Task 4's <c>ProjectResponse</c> - replace both on
    /// activation.
    /// </summary>
    public IDictionary<Guid, List<FakeProject>> Projects { get; } = new Dictionary<Guid, List<FakeProject>>();

    /// <summary>
    /// Whether a Project creation is refused <c>409 bound-reached</c> for <c>projects-per-space</c>, limit
    /// 50, as the Host refuses the 51st. Story 2.1.
    /// </summary>
    public bool ProjectBoundReached { get; set; }

    /// <summary>
    /// Every request seen, in order.
    /// </summary>
    public IReadOnlyCollection<FakeRequest> Requests => _requests;

    /// <summary>
    /// Completes with the first sign-in submission seen.
    /// </summary>
    public Task<FakeRequest> SignInSeen => _signInSeen.Task;

    /// <summary>
    /// Completes with the first sign-out seen.
    /// </summary>
    public Task<FakeRequest> SignOutSeen => _signOutSeen.Task;

    /// <summary>
    /// Holds the next answer about <paramref name="spaceId"/> until <see cref="Release"/>.
    /// </summary>
    /// <returns>A task that completes when the held request has arrived and is waiting.</returns>
    public Task HoldAsync(Guid spaceId)
    {
        _held[spaceId] = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var arrived = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _arrived[spaceId] = arrived;

        return arrived.Task;
    }

    /// <summary>
    /// Lets a held answer go.
    /// </summary>
    public void Release(Guid spaceId)
    {
        if (_held.TryRemove(spaceId, out var held))
        {
            held.TrySetResult();
        }
    }

    /// <summary>
    /// Holds the next Project list answer for <paramref name="spaceId"/> until
    /// <see cref="ReleaseProjects"/>. Story 2.1.
    /// </summary>
    /// <returns>A task that completes when the held request has arrived and is waiting.</returns>
    public Task HoldProjectsAsync(Guid spaceId)
    {
        _heldProjects[spaceId] = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var arrived = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _arrivedProjects[spaceId] = arrived;

        return arrived.Task;
    }

    /// <summary>
    /// Lets a held Project list answer go.
    /// </summary>
    public void ReleaseProjects(Guid spaceId)
    {
        if (_heldProjects.TryRemove(spaceId, out var held))
        {
            held.TrySetResult();
        }
    }

    /// <summary>
    /// Whether any request named <paramref name="spaceId"/> in its path.
    /// </summary>
    public bool WasRequested(Guid spaceId) =>
        _requests.Any(request => request.Path.Contains(spaceId.ToString("D"), StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Routes every API request the page makes, on any origin, to this fake.
    /// </summary>
    public Task InstallAsync(IPage page) => page.RouteAsync("**/api/v1/**", AnswerAsync);

    private async Task AnswerAsync(IRoute route)
    {
        var request = route.Request;
        var headers = await request.AllHeadersAsync().ConfigureAwait(false);
        var path = new Uri(request.Url).AbsolutePath;
        var seen = new FakeRequest(request.Method, path, headers, request.PostData);

        _requests.Enqueue(seen);

        var origin = headers.TryGetValue("origin", out var value) ? value : "*";

        var (status, body) = await DecideAsync(seen).ConfigureAwait(false);

        await route.FulfillAsync(new RouteFulfillOptions
        {
            Status = status,
            ContentType = status >= 400 ? "application/problem+json" : "application/json",
            Body = body,
            Headers = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["Access-Control-Allow-Origin"] = origin,
                ["Access-Control-Allow-Credentials"] = "true",
                ["Access-Control-Allow-Headers"] = $"content-type, {Antiforgery.HeaderName}, {ProjectRouteScaffold.IdempotencyKeyHeader}",
                ["Access-Control-Allow-Methods"] = "GET, POST, PATCH, DELETE",
                ["Vary"] = "Origin",
            },
        }).ConfigureAwait(false);
    }

    private async Task<(int Status, string Body)> DecideAsync(FakeRequest request)
    {
        const int noContent = 204;

        switch (request.Method, request.Path)
        {
            case ("OPTIONS", _):
                return (noContent, string.Empty);

            case ("GET", var path) when path == SessionRoutes.AntiforgeryToken:
                return (200, JsonSerializer.Serialize(new AntiforgeryTokenResponse(IssuedToken), JsonOptions));

            case ("GET", var path) when path == SessionRoutes.Current:
                return SignedIn ? (noContent, string.Empty) : (401, Problem(ProblemTypes.AuthenticationFailed));

            case ("POST", var path) when path == SessionRoutes.Authenticate:
                SignedIn = true;
                _signInSeen.TrySetResult(request);
                return (noContent, string.Empty);

            case ("DELETE", var path) when path == SessionRoutes.Authenticate && RefuseFirstSignOut:
                RefuseFirstSignOut = false;
                return (400, Problem(ProblemTypes.AntiforgeryTokenMissing));

            case ("DELETE", var path) when path == SessionRoutes.Authenticate:
                SignedIn = false;
                _signOutSeen.TrySetResult(request);
                return (noContent, string.Empty);

            case ("GET", var path) when path == SpaceRoutes.Directory:
                return (200, JsonSerializer.Serialize(Switcher.Select(space => new SpaceIdentityResponse(space.Id, space.Name)), JsonOptions));

            // Story 1.8 - the Account-settings Token routes, answered with the contract types.
            case ("GET", var path) when path == ApiTokenRoutes.List:
                return (200, JsonSerializer.Serialize(ApiTokens.Select(TokenRow), JsonOptions));

            case ("POST", var path) when TryIssue(path, out var issueSpace):
                return Issue(issueSpace, request.Body);

            case ("DELETE", var path) when path.StartsWith(ApiTokenRoutes.List + "/", StringComparison.Ordinal)
                && Guid.TryParse(path[(ApiTokenRoutes.List.Length + 1)..], out var revoked):
                _ = ApiTokens.Remove(ApiTokens.FirstOrDefault(token => token.Id == revoked) ?? FakeApiToken.None);
                return (noContent, string.Empty);

            // Story 2.1 - the Project routes.
            case ("GET", var path) when TryProjects(path, out var listSpace):
                return await ListProjectsAsync(listSpace).ConfigureAwait(false);

            case ("POST", var path) when TryProjects(path, out var createSpace):
                return CreateProject(createSpace, request.Body);

            case ("PATCH", var path) when TryProject(path, out var renameSpace, out var renamed):
                return RenameProject(renameSpace, renamed, request.Body);

            case ("GET", var path) when TryActiveSpace(path, out var spaceId):
                if (_held.TryGetValue(spaceId, out var held))
                {
                    if (_arrived.TryRemove(spaceId, out var arrived))
                    {
                        arrived.TrySetResult();
                    }

                    await held.Task.ConfigureAwait(false);
                }

                var listed = Switcher.FirstOrDefault(space => space.Id == spaceId);

                return Roles.TryGetValue(spaceId, out var role) && listed.Id == spaceId
                    ? (200, JsonSerializer.Serialize(new ActiveSpaceResponse(spaceId, listed.Name, role), JsonOptions))
                    : (404, Problem(ProblemTypes.NotAvailable));

            default:
                return (404, Problem(ProblemTypes.NotAvailable));
        }
    }

    private (int Status, string Body) Issue(Guid spaceId, string? body)
    {
        var listed = Switcher.FirstOrDefault(space => space.Id == spaceId);

        if (listed.Id != spaceId)
        {
            return (404, Problem(ProblemTypes.NotAvailable));
        }

        var name = (JsonSerializer.Deserialize<IssueApiTokenRequest>(body ?? "{}", JsonOptions)?.Name ?? string.Empty).Trim();

        // The Host's Q8 rule, as the Host answers it.
        if (name.Length is < 1 or > 100)
        {
            return (400, Problem(ProblemTypes.InvalidApiToken));
        }

        var token = new FakeApiToken(Guid.NewGuid(), name, spaceId, listed.Name, DateTimeOffset.UtcNow);
        ApiTokens.Add(token);

        var issued = new IssuedApiTokenResponse(
            token.Id,
            token.Name,
            new SpaceIdentityResponse(token.SpaceId, token.SpaceName),
            token.CreatedAt,
            IssuedSecret);

        return (201, JsonSerializer.Serialize(issued, JsonOptions));
    }

    private async Task<(int Status, string Body)> ListProjectsAsync(Guid spaceId)
    {
        if (_heldProjects.TryGetValue(spaceId, out var held))
        {
            if (_arrivedProjects.TryRemove(spaceId, out var arrived))
            {
                arrived.TrySetResult();
            }

            await held.Task.ConfigureAwait(false);
        }

        return Projects.TryGetValue(spaceId, out var projects)
            ? (200, JsonSerializer.Serialize(projects.OrderBy(project => project.Name, StringComparer.Ordinal).ThenBy(project => project.Id), JsonOptions))
            : (200, "[]");
    }

    private (int Status, string Body) CreateProject(Guid spaceId, string? body)
    {
        if (ProjectBoundReached)
        {
            return (409, JsonSerializer.Serialize(new ProblemResponse(ProblemTypes.BoundReached, Errors: null, Bound: "projects-per-space", Limit: 50), JsonOptions));
        }

        if (!TryProjectName(body, out var name))
        {
            return (400, Problem(ProjectRouteScaffold.InvalidProjectName));
        }

        var project = new FakeProject(Guid.NewGuid(), name, DateTimeOffset.UtcNow);

        if (!Projects.TryGetValue(spaceId, out var projects))
        {
            projects = [];
            Projects[spaceId] = projects;
        }

        projects.Add(project);

        return (201, JsonSerializer.Serialize(project, JsonOptions));
    }

    private (int Status, string Body) RenameProject(Guid spaceId, Guid projectId, string? body)
    {
        var projects = Projects.TryGetValue(spaceId, out var held) ? held : [];
        var index = projects.FindIndex(project => project.Id == projectId);

        if (index < 0)
        {
            return (404, Problem(ProblemTypes.NotAvailable));
        }

        if (!TryProjectName(body, out var name))
        {
            return (400, Problem(ProjectRouteScaffold.InvalidProjectName));
        }

        projects[index] = projects[index] with { Name = name };

        return (200, JsonSerializer.Serialize(projects[index], JsonOptions));
    }

    /// <summary>
    /// The Host's Q3 rule, as the Host answers it: trimmed, 1-100 characters.
    /// </summary>
    private static bool TryProjectName(string? body, out string name)
    {
        using var document = JsonDocument.Parse(string.IsNullOrEmpty(body) ? "{}" : body);

        name = (document.RootElement.TryGetProperty("name", out var value) ? value.GetString() ?? string.Empty : string.Empty).Trim();

        return name.Length is >= 1 and <= 100;
    }

    private static bool TryProjects(string path, out Guid spaceId)
    {
        var prefix = SpaceRoutes.Directory + "/";

        spaceId = Guid.Empty;

        return path.StartsWith(prefix, StringComparison.Ordinal)
            && Guid.TryParse(path[prefix.Length..].Split('/')[0], out spaceId)
            && path == ProjectRouteScaffold.ListIn(spaceId);
    }

    private static bool TryProject(string path, out Guid spaceId, out Guid projectId)
    {
        var segments = path.Split('/');

        projectId = Guid.Empty;

        return TryProjects(string.Join('/', segments[..^1]), out spaceId)
            && Guid.TryParse(segments[^1], out projectId)
            && path == ProjectRouteScaffold.ProjectOf(spaceId, projectId);
    }

    private static ApiTokenResponse TokenRow(FakeApiToken token) =>
        new(token.Id, token.Name, new SpaceIdentityResponse(token.SpaceId, token.SpaceName), token.CreatedAt);

    private static bool TryIssue(string path, out Guid spaceId)
    {
        var prefix = SpaceRoutes.Directory + "/";

        spaceId = Guid.Empty;

        return path.StartsWith(prefix, StringComparison.Ordinal)
            && Guid.TryParse(path[prefix.Length..].Split('/')[0], out spaceId)
            && path == ApiTokenRoutes.IssueIn(spaceId);
    }

    private static bool TryActiveSpace(string path, out Guid spaceId)
    {
        var prefix = SpaceRoutes.Directory + "/";

        spaceId = Guid.Empty;

        return path.StartsWith(prefix, StringComparison.Ordinal)
            && Guid.TryParse(path[prefix.Length..], out spaceId)
            && path == SpaceRoutes.ActiveOf(spaceId);
    }

    private static string Problem(string type) =>
        JsonSerializer.Serialize(new ProblemResponse(type, Errors: null), JsonOptions);
}

/// <summary>
/// One request the fake answered.
/// </summary>
/// <param name="Method">The HTTP method.</param>
/// <param name="Path">The path, without the query.</param>
/// <param name="Headers">Every header, lower-cased by the browser.</param>
/// <param name="Body">The request body, or <c>null</c>.</param>
internal sealed record FakeRequest(string Method, string Path, IReadOnlyDictionary<string, string> Headers, string? Body = null)
{
    /// <summary>
    /// A header's value by name, ignoring case, or <c>null</c>.
    /// </summary>
    /// <param name="name">The header name.</param>
    /// <returns>The value.</returns>
    public string? Header(string name) =>
        Headers.FirstOrDefault(header => string.Equals(header.Key, name, StringComparison.OrdinalIgnoreCase)).Value;
}

/// <summary>
/// One API Token as the fake's Account-settings list holds it. Story 1.8.
/// </summary>
/// <param name="Id">The Token's id.</param>
/// <param name="Name">Its name.</param>
/// <param name="SpaceId">The Space it is bound to.</param>
/// <param name="SpaceName">That Space's name.</param>
/// <param name="CreatedAt">When it was issued.</param>
internal sealed record FakeApiToken(Guid Id, string Name, Guid SpaceId, string SpaceName, DateTimeOffset CreatedAt)
{
    /// <summary>
    /// Matches no Token, for a revoke of an id that is not held.
    /// </summary>
    public static FakeApiToken None { get; } = new(Guid.Empty, string.Empty, Guid.Empty, string.Empty, DateTimeOffset.MinValue);
}

/// <summary>
/// One Project as the fake serves it - the shape of story 2.1 Task 4's <c>ProjectResponse</c>
/// (<c>{id, name, createdAt}</c>). Replace with the contract type on activation.
/// </summary>
/// <param name="Id">The Project's id.</param>
/// <param name="Name">Its name.</param>
/// <param name="CreatedAt">When it was created.</param>
internal sealed record FakeProject(Guid Id, string Name, DateTimeOffset CreatedAt);
