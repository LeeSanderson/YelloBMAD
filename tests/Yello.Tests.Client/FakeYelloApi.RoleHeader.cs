using Yello.Contracts;
using Yello.Contracts.Spaces;
using Yello.Tests.Shared;

namespace Yello.Tests.Client;

/// <summary>
/// The Role header as the Host answers it: every Space-scoped answer that resolved a Membership - a <c>2xx</c>, or a
/// <c>403 insufficient-role</c> - carries the Role the fake holds for that Space in <see cref="Roles"/> at the moment it
/// answers, and nothing else carries it. Changing <c>Roles[space]</c> mid-case is therefore seen on the very next
/// Space-scoped request of any kind, as a demotion made elsewhere would be. Also a hold on the next Space-scoped read, so a
/// case can land an answer late - after the tab has switched Space.
/// </summary>
/// <remarks>
/// <para>
/// <b>Exposed across origins.</b> The fake answers a cross-origin client, and a browser hides every non-safelisted response
/// header from <c>fetch</c> unless the answer exposes it, so each answer names the header in
/// <c>Access-Control-Expose-Headers</c>, as the Host's cross-origin policy must.
/// </para>
/// <para>
/// <b>On by default</b> (<see cref="EchoesRole"/>): the client reads no such header today, so every earlier case behaves as it
/// did; once it does, every case sees the header the Host will send.
/// </para>
/// </remarks>
internal sealed partial class FakeYelloApi
{
    private SpaceReadHold? _spaceReadHold;

    /// <summary>
    /// Whether Space-scoped answers carry the Role header. A case that needs an answer from an older Host turns it off.
    /// </summary>
    public bool EchoesRole { get; set; } = true;

    /// <summary>
    /// Holds the next <c>GET</c> under <paramref name="spaceId"/> whose path ends with <paramref name="pathSuffix"/> (any,
    /// when <c>null</c>) until <see cref="ReleaseSpaceRead"/>. The answer, and its Role header, are decided at release.
    /// </summary>
    /// <param name="spaceId">The Space the read names.</param>
    /// <param name="pathSuffix">The path's ending, for example <c>/board</c>, or <c>null</c> for any read in the Space.</param>
    /// <returns>A task that completes when the held read has arrived and is waiting.</returns>
    public Task HoldSpaceReadAsync(Guid spaceId, string? pathSuffix = null)
    {
        var hold = new SpaceReadHold(spaceId, pathSuffix);

        _spaceReadHold = hold;

        return hold.Arrived.Task;
    }

    /// <summary>
    /// Lets the held read answer: as the fake would answer it then, or with <paramref name="answer"/> instead.
    /// </summary>
    /// <param name="answer">A status and body to answer with instead, or <c>null</c>.</param>
    public void ReleaseSpaceRead((int Status, string Body)? answer = null)
    {
        if (Interlocked.Exchange(ref _spaceReadHold, null) is { } hold)
        {
            hold.Override = answer;
            hold.Released.TrySetResult();
        }
    }

    /// <summary>
    /// Every request the page has sent so far, in order - for counting what an idle interval sends.
    /// </summary>
    public int RequestCount => _requests.Count;

    /// <summary>
    /// When <paramref name="request"/> is the held read, waits for its release and returns the answer to give instead, if
    /// the release named one.
    /// </summary>
    private async Task<(bool Held, (int Status, string Body)? Override)> AwaitSpaceReadHoldAsync(FakeRequest request)
    {
        var hold = _spaceReadHold;

        if (hold is null || !hold.Matches(request) || !hold.Arrived.TrySetResult())
        {
            return (false, null);
        }

        await hold.Released.Task.ConfigureAwait(false);

        return (true, hold.Override);
    }

    /// <summary>
    /// The Role header's entries for one answer: the Role the fake holds for the path's Space, on a <c>2xx</c> or an
    /// <c>insufficient-role</c> <c>403</c> under a Space the Account holds; nothing on any other answer.
    /// </summary>
    private void AddRoleHeader(Dictionary<string, string> headers, FakeRequest request, int status, string body)
    {
        headers["Access-Control-Expose-Headers"] = RoleHeaderWire.HeaderName;

        if (!EchoesRole
            || string.Equals(request.Method, "OPTIONS", StringComparison.Ordinal)
            || SpaceOf(request.Path) is not { } spaceId
            || !Roles.TryGetValue(spaceId, out var role))
        {
            return;
        }

        var judged = status is >= 200 and < 300
            || (status == 403 && body.Contains(ProblemTypes.InsufficientRole, StringComparison.Ordinal));

        if (judged)
        {
            headers[RoleHeaderWire.HeaderName] = role;
        }
    }

    /// <summary>
    /// The Space a path is scoped to: the id after <c>/api/v1/spaces/</c>, or <c>null</c> for an Account-scoped path.
    /// </summary>
    private static Guid? SpaceOf(string path)
    {
        var prefix = SpaceRoutes.Directory + "/";

        if (!path.StartsWith(prefix, StringComparison.Ordinal))
        {
            return null;
        }

        var rest = path[prefix.Length..];
        var end = rest.IndexOf('/', StringComparison.Ordinal);
        var segment = end < 0 ? rest : rest[..end];

        return Guid.TryParse(segment, out var spaceId) ? spaceId : null;
    }

    private sealed class SpaceReadHold(Guid spaceId, string? pathSuffix)
    {
        public Guid SpaceId { get; } = spaceId;

        public string? PathSuffix { get; } = pathSuffix;

        public TaskCompletionSource Arrived { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource Released { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public (int Status, string Body)? Override { get; set; }

        public bool Matches(FakeRequest request) =>
            string.Equals(request.Method, "GET", StringComparison.Ordinal)
            && SpaceOf(request.Path) == SpaceId
            && (PathSuffix is null || request.Path.EndsWith(PathSuffix, StringComparison.Ordinal));
    }
}
