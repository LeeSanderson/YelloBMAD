using System.Net.Http.Json;
using System.Text.Json;
using Yello.Contracts.Memberships;

namespace Yello.Client.Spaces;

/// <summary>
/// Reads the active Space's Assignee candidates and labels them. Holds nothing: the Board and the List keep what it answers
/// as Space-scoped state of their own and purge it with the Space, as they do the Space's Labels.
/// </summary>
/// <remarks>
/// The labels are computed over the whole list at once, so two look-alike names are told apart wherever either is shown.
/// </remarks>
public static class SpaceAssignees
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// Labels the candidates, in the order given.
    /// </summary>
    /// <param name="candidates">The Space's candidates, as the server answered them.</param>
    /// <returns>Each candidate's label and initials.</returns>
    public static IReadOnlyList<AssigneeChoice> Labelled(IReadOnlyList<AssigneeResponse> candidates)
    {
        ArgumentNullException.ThrowIfNull(candidates);

        var labels = MembershipLabels.Disambiguate([.. candidates.Select(candidate => (candidate.MembershipId, candidate.DisplayName))]);

        return [.. candidates.Select(candidate => new AssigneeChoice(candidate.MembershipId, labels[candidate.MembershipId], MembershipLabels.Initials(candidate.DisplayName)))];
    }

    /// <summary>
    /// Reads and labels the Space's candidates.
    /// </summary>
    /// <param name="http">The API client.</param>
    /// <param name="spaceId">The active Space.</param>
    /// <param name="token">The active Space's token.</param>
    /// <returns>The candidates, or <c>null</c> when the read was not answered with them.</returns>
    public static Task<IReadOnlyList<AssigneeChoice>?> ReadAsync(HttpClient http, Guid spaceId, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(http);

        return ReadCoreAsync(http, spaceId, token);
    }

    private static async Task<IReadOnlyList<AssigneeChoice>?> ReadCoreAsync(HttpClient http, Guid spaceId, CancellationToken token)
    {
        try
        {
            using var response = await http.GetAsync(new Uri(AssigneeRoutes.ListOf(spaceId), UriKind.Relative), token);

            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            return await response.Content.ReadFromJsonAsync<AssigneeListResponse>(JsonOptions, token) is { } list
                ? Labelled(list.Assignees)
                : null;
        }
        catch (Exception exception) when (exception is HttpRequestException or JsonException
            || (exception is OperationCanceledException && !token.IsCancellationRequested))
        {
            // The candidates only name Assignees: a read that fails must not take the Board or the List down with it.
            // A cancelled Space token still propagates, as the Labels read's does.
            return null;
        }
    }
}
