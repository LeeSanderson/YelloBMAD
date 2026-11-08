using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Yello.Client.Spaces;
using Yello.Contracts;
using Yello.Contracts.Labels;

namespace Yello.Client.Components;

/// <summary>
/// The Space's Labels and Assignee candidates as one Board holds them: the cards and the panel name what a Task carries
/// from these. Space-scoped state, cleared with the Board's purge.
/// </summary>
/// <remarks>
/// <para>
/// <b>A cold load reads both before the Board and shows them with it</b>: <see cref="ReadAsync"/> holds what it read until
/// <see cref="Show"/>, so nothing read for a Board that never drew is ever shown. The Labels read decides the Board - its
/// refusal is the page's - while a candidates read that is not answered only leaves the cards without avatars.
/// </para>
/// <para>
/// <b>Apart from the Board's own class</b> only to keep that one inside its dependency budget (S1200).
/// </para>
/// </remarks>
/// <param name="http">The API client.</param>
public sealed class BoardSpaceLists(HttpClient http)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private IReadOnlyList<LabelResponse> _readLabels = [];

    private IReadOnlyList<AssigneeChoice> _readAssignees = [];

    /// <summary>
    /// The Space's Labels, in the server's order.
    /// </summary>
    public IReadOnlyList<LabelResponse> Labels { get; private set; } = [];

    /// <summary>
    /// The Space's Labels by id, for the cards.
    /// </summary>
    public IReadOnlyDictionary<Guid, LabelResponse> LabelsById { get; private set; } = new Dictionary<Guid, LabelResponse>();

    /// <summary>
    /// The Space's Assignee candidates, labelled over the whole list, in join order.
    /// </summary>
    public IReadOnlyList<AssigneeChoice> Assignees { get; private set; } = [];

    /// <summary>
    /// The candidates by Membership, for the cards.
    /// </summary>
    public IReadOnlyDictionary<Guid, AssigneeChoice> AssigneesById { get; private set; } = new Dictionary<Guid, AssigneeChoice>();

    /// <summary>
    /// Reads the Labels and the candidates for a cold load, holding them until <see cref="Show"/>.
    /// </summary>
    /// <param name="spaceId">The active Space.</param>
    /// <param name="onRefused">Raised with the Labels read's refusal; never for a <c>401</c>, which ends the Session.</param>
    /// <param name="token">The active Space's token.</param>
    /// <returns>Whether the Labels were read; <c>false</c> once refused or once the Space has been left.</returns>
    public async Task<bool> ReadAsync(Guid spaceId, Func<ProblemResponse?, Task> onRefused, CancellationToken token)
    {
        using var response = await http.GetAsync(new Uri(LabelRoutes.LabelsOf(spaceId), UriKind.Relative), token);

        if (token.IsCancellationRequested)
        {
            return false;
        }

        if (!response.IsSuccessStatusCode)
        {
            if (response.StatusCode != HttpStatusCode.Unauthorized)
            {
                await onRefused(await SpaceWrites.ProblemOfAsync(response, token));
            }

            return false;
        }

        _readLabels = await response.Content.ReadFromJsonAsync<List<LabelResponse>>(JsonOptions, token) ?? [];
        _readAssignees = await SpaceAssignees.ReadAsync(http, spaceId, token) ?? [];

        return !token.IsCancellationRequested;
    }

    /// <summary>
    /// Shows what the cold load read, with the Board it was read for.
    /// </summary>
    public void Show()
    {
        UseLabels(_readLabels);
        UseAssignees(_readAssignees);
    }

    /// <summary>
    /// Forgets everything: the Space is being left.
    /// </summary>
    public void Clear()
    {
        _readLabels = [];
        _readAssignees = [];
        UseLabels([]);
        UseAssignees([]);
    }

    /// <summary>
    /// Whether a Membership has a candidate here.
    /// </summary>
    /// <param name="membershipId">The Membership.</param>
    /// <returns>Whether it does.</returns>
    public bool HoldsAssignee(Guid membershipId) => AssigneesById.ContainsKey(membershipId);

    /// <summary>
    /// How a candidate is named, or <c>null</c> when this Board holds none for the Membership.
    /// </summary>
    /// <param name="membershipId">The Membership.</param>
    /// <returns>Its label.</returns>
    public string? LabelOf(Guid membershipId) => AssigneesById.GetValueOrDefault(membershipId)?.Label;

    /// <summary>
    /// Reads the Labels again. A read that fails, or answers after the Board has moved on, leaves them as they were.
    /// </summary>
    /// <param name="spaceId">The active Space.</param>
    /// <param name="current">Whether the Board that asked is still the one shown.</param>
    /// <param name="token">The active Space's token.</param>
    /// <returns>A task.</returns>
    public async Task RefreshLabelsAsync(Guid spaceId, Func<bool> current, CancellationToken token)
    {
        try
        {
            using var response = await http.GetAsync(new Uri(LabelRoutes.LabelsOf(spaceId), UriKind.Relative), token);

            if (!response.IsSuccessStatusCode || !current() || token.IsCancellationRequested)
            {
                return;
            }

            if (await response.Content.ReadFromJsonAsync<List<LabelResponse>>(JsonOptions, token) is { } labels && current())
            {
                UseLabels(labels);
            }
        }
        catch (Exception exception) when (exception is OperationCanceledException or HttpRequestException or JsonException)
        {
            // Left as it was; see above.
        }
    }

    /// <summary>
    /// Reads the candidates again. A read that fails, or answers after the Board has moved on, leaves them as they were.
    /// </summary>
    /// <param name="spaceId">The active Space.</param>
    /// <param name="current">Whether the Board that asked is still the one shown.</param>
    /// <param name="token">The active Space's token.</param>
    /// <returns>A task.</returns>
    public async Task RefreshAssigneesAsync(Guid spaceId, Func<bool> current, CancellationToken token)
    {
        try
        {
            if (await SpaceAssignees.ReadAsync(http, spaceId, token) is { } assignees && current() && !token.IsCancellationRequested)
            {
                UseAssignees(assignees);
            }
        }
        catch (Exception exception) when (exception is OperationCanceledException or HttpRequestException or JsonException)
        {
            // Left as it was; see above.
        }
    }

    private void UseLabels(IReadOnlyList<LabelResponse> labels)
    {
        Labels = labels;
        LabelsById = labels.ToDictionary(label => label.Id);
    }

    private void UseAssignees(IReadOnlyList<AssigneeChoice> assignees)
    {
        Assignees = assignees;
        AssigneesById = assignees.ToDictionary(assignee => assignee.MembershipId);
    }
}
