using System.Text.Json;
using System.Text.RegularExpressions;
using Yello.Contracts;
using Yello.Domain.Tasks;

namespace Yello.Host.Endpoints;

/// <summary>
/// Reads a Task edit's body as a JSON merge patch (story 2.3, open question 1 (a)), inside the endpoint and
/// after the pipeline has run, as every Space-scoped body is read.
/// </summary>
/// <remarks>
/// <para>
/// <b>Read as a document, not bound to <c>EditTaskRequest</c></b>, because a bound record cannot tell a member
/// the body does not name (unchanged) from one it sets to <c>null</c> (the due date cleared; the title or
/// Status refused).
/// </para>
/// <para>
/// <b>Every refusal is a <c>400</c> with a stable type per field</b> (open question 5 (a)), never a
/// <c>500</c>, and none echoes what was sent. A member this route does not read is ignored; a body naming
/// none of the four is refused.
/// </para>
/// <para>
/// <b><c>labelIds</c> is the whole set</b> (story 2.4, open question 1 (a)): <c>[]</c> clears it; <c>null</c>, a
/// value that is not an array, or an array longer than <see cref="MostLabelIds"/> is the shape refusal
/// <c>invalid-task-edit</c>; an entry that is not an id, or an id named twice, is <c>invalid-task-labels</c> - the
/// same type, and the same bytes, as a Label that is not the Space's, which only the store can tell.
/// </para>
/// </remarks>
internal static partial class TaskEditBody
{
    private const string TitleMember = "title";

    private const string StatusMember = "statusId";

    private const string DueDateMember = "dueDate";

    private const string LabelsMember = "labelIds";

    private static readonly string[] AttributeMembers = [TitleMember, StatusMember, DueDateMember];

    /// <summary>
    /// The most entries a <c>labelIds</c> array may hold: an input-shape guard, so a hand-written body cannot make the
    /// server parse and lock without limit - not an NFR-8 bound, which would be the PM's to set (story 2.4, open
    /// question 6). No Task carries anywhere near this many Labels.
    /// </summary>
    private const int MostLabelIds = 500;

    /// <summary>
    /// The longest text a due date can be: an ISO 8601 instant with seven fractional digits and an offset is
    /// 33 characters. A longer text is refused before the offset pattern scans it.
    /// </summary>
    private const int LongestDueDate = 40;

    /// <summary>
    /// Reads the edit, or the refusal that answers it.
    /// </summary>
    /// <param name="request">The request.</param>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    /// <returns>The edit, or a refusal - exactly one of the two.</returns>
    internal static async Task<(TaskEdit? Edit, IResult? Refusal)> ReadAsync(HttpRequest request, CancellationToken cancellationToken)
    {
        using var document = await ParseAsync(request, cancellationToken).ConfigureAwait(false);

        return document?.RootElement is { ValueKind: JsonValueKind.Object } root
            ? Read(root)
            : (null, InvalidEdit());
    }

    /// <summary>
    /// <c>400 invalid-task-status</c>: the Status is not one of the Task's Project's effective set. One body for
    /// a malformed id, a random id and another Space's real id alike (I-13).
    /// </summary>
    /// <returns>The refusal.</returns>
    internal static IResult InvalidStatus() =>
        Results.Problem(
            statusCode: StatusCodes.Status400BadRequest,
            type: ProblemTypes.InvalidTaskStatus,
            title: "That Status is not one of this Task's Project's Statuses.");

    /// <summary>
    /// <c>400 invalid-task-labels</c>: a Label of the set is not one of the Space's. One body for a malformed id, a
    /// repeated id, a random id, another Space's real id and a Label deleted under the edit alike (I-13).
    /// </summary>
    /// <returns>The refusal.</returns>
    internal static IResult InvalidLabels() =>
        Results.Problem(
            statusCode: StatusCodes.Status400BadRequest,
            type: ProblemTypes.InvalidTaskLabels,
            title: "Those Labels are not all this Space's.");

    private static (TaskEdit? Edit, IResult? Refusal) Read(JsonElement root)
    {
        var hasLabels = root.TryGetProperty(LabelsMember, out var labelsElement);

        if (!hasLabels && !Array.Exists(AttributeMembers, member => root.TryGetProperty(member, out _)))
        {
            return (null, InvalidEdit());
        }

        var (attributes, refusal) = ReadAttributes(root);

        if (refusal is not null)
        {
            return (null, refusal);
        }

        IReadOnlyList<Guid>? labelIds = null;

        if (hasLabels && ReadLabels(labelsElement, out labelIds) is { } labelsRefusal)
        {
            return (null, labelsRefusal);
        }

        return (attributes! with { ChangesLabels = hasLabels, LabelIds = labelIds }, null);
    }

    /// <summary>
    /// The title, Status and due date members - story 2.3's - or the refusal of the first that is not one.
    /// </summary>
    private static (TaskEdit? Edit, IResult? Refusal) ReadAttributes(JsonElement root)
    {
        var hasTitle = root.TryGetProperty(TitleMember, out var titleElement);
        var hasStatus = root.TryGetProperty(StatusMember, out var statusElement);
        var hasDueDate = root.TryGetProperty(DueDateMember, out var dueDateElement);

        string? title = null;
        Guid? statusId = null;
        DateTimeOffset? dueDate = null;

        if (hasTitle && !TryTitle(titleElement, out title))
        {
            return (null, InvalidTitle());
        }

        if (hasStatus && !TryStatus(statusElement, out statusId))
        {
            return (null, InvalidStatus());
        }

        if (hasDueDate && !TryDueDate(dueDateElement, out dueDate))
        {
            return (null, InvalidDueDate());
        }

        return (new TaskEdit(title, statusId, hasDueDate, dueDate), null);
    }

    private static IResult? ReadLabels(JsonElement element, out IReadOnlyList<Guid>? labelIds)
    {
        labelIds = null;

        if (element.ValueKind != JsonValueKind.Array || element.GetArrayLength() > MostLabelIds)
        {
            return InvalidEdit();
        }

        var ids = new List<Guid>(element.GetArrayLength());

        foreach (var entry in element.EnumerateArray())
        {
            if (entry.ValueKind != JsonValueKind.String || !entry.TryGetGuid(out var id) || ids.Contains(id))
            {
                return InvalidLabels();
            }

            ids.Add(id);
        }

        labelIds = ids;

        return null;
    }

    private static async Task<JsonDocument?> ParseAsync(HttpRequest request, CancellationToken cancellationToken)
    {
        if (!request.HasJsonContentType())
        {
            return null;
        }

        try
        {
            return await JsonDocument.ParseAsync(request.Body, default, cancellationToken).ConfigureAwait(false);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static bool TryTitle(JsonElement element, out string? title)
    {
        title = null;

        if (element.ValueKind != JsonValueKind.String || !TaskTitle.TryNormalise(element.GetString(), out var normalised))
        {
            return false;
        }

        title = normalised;

        return true;
    }

    private static bool TryStatus(JsonElement element, out Guid? statusId)
    {
        statusId = null;

        if (element.ValueKind != JsonValueKind.String || !element.TryGetGuid(out var parsed))
        {
            return false;
        }

        statusId = parsed;

        return true;
    }

    private static bool TryDueDate(JsonElement element, out DateTimeOffset? dueDate)
    {
        dueDate = null;

        // null clears the due date; it is not a refusal.
        if (element.ValueKind == JsonValueKind.Null)
        {
            return true;
        }

        return element.ValueKind == JsonValueKind.String
            && HasAnOffset(element.GetString())
            && element.TryGetDateTimeOffset(out var submitted)
            && TaskDueDate.TryNormalise(submitted, out dueDate);
    }

    /// <summary>
    /// Whether an ISO 8601 text states its offset (<c>Z</c> or <c>±hh:mm</c>). Without one, the reader would
    /// assume the server's own zone, and a due date must never be a zone-less value (AR-34).
    /// </summary>
    private static bool HasAnOffset(string? text) =>
        text is { Length: <= LongestDueDate } && OffsetSuffix.IsMatch(text);

    [GeneratedRegex(@"(?:[Zz]|[+-][0-9]{2}:[0-9]{2})\z", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 100)]
    private static partial Regex OffsetSuffix { get; }

    private static IResult InvalidEdit() =>
        Results.Problem(
            statusCode: StatusCodes.Status400BadRequest,
            type: ProblemTypes.InvalidTaskEdit,
            title: "A Task edit names its title, Status, due date or Labels.");

    private static IResult InvalidTitle() =>
        Results.Problem(
            statusCode: StatusCodes.Status400BadRequest,
            type: ProblemTypes.InvalidTaskTitle,
            title: "A Task's title is 1 to 200 characters.");

    private static IResult InvalidDueDate() =>
        Results.Problem(
            statusCode: StatusCodes.Status400BadRequest,
            type: ProblemTypes.InvalidDueDate,
            title: "A due date is a date between 2000 and 2100.");
}
