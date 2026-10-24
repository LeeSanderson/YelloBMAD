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
/// lists (AC11), and the refusal page's route back goes through the entry point - so no Space is ever
/// reached from a remembered last one (UX-DR32). Nothing records which Space was last visited.
/// </para>
/// <para>
/// <b>Held in a field, never in browser storage</b> (<c>ClientStorageBanTests</c>). It is
/// Account-scoped identity rather than Space-scoped content, so it survives a Space switch; it is
/// dropped on sign-out and when the Session ends, through the Session's purge.
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
    /// Drops the answer - on sign-out and when the Session ends.
    /// </summary>
    public void Forget()
    {
        _generation++;
        Current = null;
    }
}
