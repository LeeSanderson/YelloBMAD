using System.Net.Http.Json;
using System.Text.Json;
using Yello.Contracts.Spaces;

namespace Yello.Client.Spaces;

/// <summary>
/// The client's only list of Space ids: the Space switcher's last answer, held in memory. FR-9;
/// AC1, AC9, AC10; UX-DR31, UX-DR32.
/// </summary>
/// <remarks>
/// <para>
/// <b>The one source a Space id may come from.</b> The Space home checks its route's id against this
/// list before it asks anything about that Space (AC10), the entry point lands on the first Space it
/// lists that has not refused this tab (AC11; story 3.2, AC12), and the refusal page's route back goes through the entry point - so no Space is ever
/// reached from a remembered last one (UX-DR32). Nothing records which Space was last visited.
/// </para>
/// <para>
/// <b>Held in a field, never in browser storage</b> (<c>ClientStorageBanTests</c>). It is
/// Account-scoped identity rather than Space-scoped content, so it survives a Space switch; it is
/// dropped on sign-out and when the Session ends, through the Session's purge.
/// </para>
/// <para>
/// <b>The Spaces that refused this tab, held only to be skipped</b> (story 3.2, AC12, open question 1 (a);
/// <c>deferred-work.md:358</c>). A listed Space that answers its arrival with a <c>403</c>, <c>404</c> or <c>409</c> is
/// recorded, and the entry point's route back lands on the first listed Space NOT recorded, so "Back to Yello" never
/// loops on the Space that just refused. The set can only exclude, never choose a destination: the destination is
/// always the switcher's answer in the order served, so this is not a remembered last Space (UX-DR32). A successful
/// arrival removes a Space from it, and <see cref="Forget"/> clears it. It is per tab and per load, in memory only.
/// </para>
/// <para>
/// <b>Refetched on arrival at the entry point, on a fresh load of a Space route, and every time the
/// switcher opens</b> - Memberships can change between two looks (epics 3 and 4).
/// </para>
/// </remarks>
/// <param name="http">The client, whose handler carries the Session cookie.</param>
public sealed class SpaceDirectory(HttpClient http)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    // The listed Spaces that refused this tab's arrival in this load (story 3.2, AC12). It only ever EXCLUDES: the
    // route back skips its members and never chooses one, so it is not a remembered last Space (UX-DR32).
    private readonly HashSet<Guid> _refusedInThisLoad = [];

    // Advanced by every Forget, so an answer requested before one is recognised when it lands.
    private int _generation;

    /// <summary>
    /// The last answer, in the order the server gave it, or <c>null</c> before the first and after
    /// the Session ends.
    /// </summary>
    public IReadOnlyList<SpaceIdentityResponse>? Current { get; private set; }

    /// <summary>
    /// Asks the server again.
    /// </summary>
    /// <param name="cancellationToken">The caller's cancellation token.</param>
    /// <returns>The new answer, or <c>null</c> when it could not be had - which the caller reports.</returns>
    public async Task<IReadOnlyList<SpaceIdentityResponse>?> RefreshAsync(CancellationToken cancellationToken)
    {
        var generation = _generation;

        try
        {
            var spaces = await http
                .GetFromJsonAsync<List<SpaceIdentityResponse>>(SpaceRoutes.Directory, JsonOptions, cancellationToken)
                .ConfigureAwait(false);

            // An answer that lands after the Session ended belongs to the Account that has gone,
            // and is dropped rather than held for whoever signs in next.
            if (generation != _generation)
            {
                return null;
            }

            Current = spaces;

            return spaces;
        }
        catch (HttpRequestException)
        {
            // The server refused or could not be reached. A 401 has already ended the Session in
            // YelloApiHandler; anything else is the caller's to report.
            return null;
        }
        catch (JsonException)
        {
            return null;
        }
        catch (OperationCanceledException)
        {
            return null;
        }
    }

    /// <summary>
    /// Whether the current answer lists a Space.
    /// </summary>
    /// <param name="spaceId">The Space.</param>
    /// <returns><c>true</c> only when the last answer named it.</returns>
    public bool Lists(Guid spaceId) => Current?.Any(space => space.Id == spaceId) == true;

    /// <summary>
    /// Renames one row of the held answer in place, after the server has renamed that Space (story 3.1, AC14, open
    /// question 7 (a)) - so the switcher shows the new name even before its next refresh lands. A Space the answer does
    /// not list is left unlisted.
    /// </summary>
    /// <param name="spaceId">The renamed Space.</param>
    /// <param name="name">Its new name, from the server's answer.</param>
    public void Renamed(Guid spaceId, string name)
    {
        if (Current is not { } current || !current.Any(space => space.Id == spaceId))
        {
            return;
        }

        Current = [.. current.Select(space => space.Id == spaceId ? space with { Name = name } : space)];
    }

    /// <summary>
    /// Drops one Space from the held answer and from the Spaces that refused this tab, after the server has deleted it,
    /// or answered that it is already gone (story 3.3, AC12). It edits the directory - the rest of the answer stays, in
    /// the order served - and never forgets it: the directory is Account-scoped and outlives the Space.
    /// </summary>
    /// <param name="spaceId">The deleted Space.</param>
    public void Removed(Guid spaceId)
    {
        _refusedInThisLoad.Remove(spaceId);

        if (Current is { } current)
        {
            Current = [.. current.Where(space => space.Id != spaceId)];
        }
    }

    /// <summary>
    /// Records that a listed Space refused this tab's arrival with its own answer (a <c>403</c>, <c>404</c> or
    /// <c>409</c>), so the entry point's route back offers a different one (story 3.2, AC12, open question 1 (a)). A
    /// timeout, a transport failure or a <c>5xx</c> is not a refusal and is not recorded.
    /// </summary>
    /// <param name="spaceId">The Space that refused.</param>
    public void Refused(Guid spaceId) => _refusedInThisLoad.Add(spaceId);

    /// <summary>
    /// Records that this tab arrived in a Space, so a Space that refused earlier in this load and has since recovered
    /// (a <c>409</c> for the concurrent-Sessions bound, say) is offered by the route back again.
    /// </summary>
    /// <param name="spaceId">The Space arrived in.</param>
    public void ArrivedIn(Guid spaceId) => _refusedInThisLoad.Remove(spaceId);

    /// <summary>
    /// Whether a Space refused this tab's arrival in this load and has not been arrived in since.
    /// </summary>
    /// <param name="spaceId">The Space.</param>
    /// <returns><c>true</c> when the route back must not offer it.</returns>
    public bool RefusedInThisLoad(Guid spaceId) => _refusedInThisLoad.Contains(spaceId);

    /// <summary>
    /// Drops the answer, and the Spaces that refused this tab - on sign-out and when the Session ends.
    /// </summary>
    public void Forget()
    {
        _generation++;
        Current = null;
        _refusedInThisLoad.Clear();
    }
}
