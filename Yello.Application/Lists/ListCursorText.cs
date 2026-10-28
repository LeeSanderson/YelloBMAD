using System.Buffers.Text;
using System.Globalization;
using System.Text;
using Yello.Domain.Labels;
using Yello.Domain.Lists;

namespace Yello.Application.Lists;

/// <summary>
/// The List View cursor on the wire: an opaque, URL-safe string the server issues, naming the sort it was issued for and
/// the last served pair (story 2.10, open question 1 (a); AC12).
/// </summary>
/// <remarks>
/// <para>
/// <b>Base64url of a fixed, versioned text form</b>: <c>1</c>, the sort's code, the Task id, a segment flag
/// (<c>v</c> for a value, <c>n</c> for the trailing segment of Tasks with none), and the sort value - newline-separated,
/// the value last so a Label name may hold any character. Opaque because a pair has no single readable value, as story
/// 2.9's position key had; FR-35 makes the position key readable, not a cursor.
/// </para>
/// <para>
/// <b>Binding the sort into the cursor makes a cursor issued for another sort detectable</b>: <see cref="TryRead"/>
/// refuses it, as it refuses anything else this route did not issue - by its text alone, never by what is stored, so the
/// refusal is a statement about the request (<c>400 invalid-list-cursor</c>). A well-formed cursor whose Task has gone is
/// read like any other: the page after it is the page after its pair.
/// </para>
/// </remarks>
public static class ListCursorText
{
    private const int Longest = 512;

    private const string Version = "1";

    private const string ValueSegment = "v";

    private const string NullSegment = "n";

    private const char Separator = '\n';

    private const int Parts = 5;

    private const int VersionPart = 0;

    private const int SortPart = 1;

    private const int TaskPart = 2;

    private const int SegmentPart = 3;

    private const int ValuePart = 4;

    private static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    /// <summary>
    /// The cursor after <paramref name="cursor"/>'s pair under <paramref name="sort"/>, as the route issues it.
    /// </summary>
    /// <param name="sort">The sort the page was read under.</param>
    /// <param name="cursor">The last served pair.</param>
    /// <returns>The cursor text.</returns>
    public static string Write(ListSort sort, ListCursor cursor)
    {
        ArgumentNullException.ThrowIfNull(cursor);

        var value = sort switch
        {
            ListSort.Status => cursor.StatusId?.ToString("D"),
            ListSort.DueDate => cursor.DueDate?.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
            ListSort.Label => cursor.FirstLabelName,
            _ => throw new ArgumentOutOfRangeException(nameof(sort), sort, "Not an offered sort."),
        };

        var text = string.Join(
            Separator,
            Version,
            ListSortCodes.Of(sort),
            cursor.TaskId.ToString("N"),
            value is null ? NullSegment : ValueSegment,
            value ?? string.Empty);

        return Base64Url.EncodeToString(Encoding.UTF8.GetBytes(text));
    }

    /// <summary>
    /// Reads a cursor sent with a page request under <paramref name="sort"/> - <c>false</c> for anything the route did not
    /// issue for that sort: not base64url, too long, another version, another sort, a malformed id, or a value the sort's
    /// column cannot hold.
    /// </summary>
    /// <param name="sort">The sort the request asks for.</param>
    /// <param name="sent">The cursor as sent.</param>
    /// <param name="cursor">The pair, when it reads.</param>
    /// <returns>Whether it reads.</returns>
    public static bool TryRead(ListSort sort, string sent, out ListCursor? cursor)
    {
        ArgumentNullException.ThrowIfNull(sent);

        cursor = null;

        if (Decode(sent) is not { } text)
        {
            return false;
        }

        var parts = text.Split(Separator, Parts);

        if (parts.Length != Parts
            || !string.Equals(parts[VersionPart], Version, StringComparison.Ordinal)
            || !string.Equals(parts[SortPart], ListSortCodes.Of(sort), StringComparison.Ordinal)
            || !Guid.TryParseExact(parts[TaskPart], "N", out var taskId))
        {
            return false;
        }

        cursor = parts[SegmentPart] switch
        {
            NullSegment => TrailingSegment(sort, taskId, parts[ValuePart]),
            ValueSegment => Valued(sort, taskId, parts[ValuePart]),
            _ => null,
        };

        return cursor is not null;
    }

    /// <summary>
    /// The cursor's text, or <c>null</c> when it is empty, too long, not base64url or not UTF-8.
    /// </summary>
    private static string? Decode(string sent)
    {
        if (sent.Length is 0 or > Longest || !Base64Url.IsValid(sent))
        {
            return null;
        }

        try
        {
            return StrictUtf8.GetString(Base64Url.DecodeFromChars(sent));
        }
        catch (FormatException)
        {
            return null;
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    /// <summary>
    /// A cursor in the trailing segment of Tasks with no sort value - never under the Status sort, and carrying no value.
    /// </summary>
    private static ListCursor? TrailingSegment(ListSort sort, Guid taskId, string value) =>
        sort != ListSort.Status && value.Length == 0 ? new ListCursor(taskId) : null;

    /// <summary>
    /// A cursor holding a sort value the sort's column can hold.
    /// </summary>
    private static ListCursor? Valued(ListSort sort, Guid taskId, string value) => sort switch
    {
        ListSort.Status when Guid.TryParseExact(value, "D", out var statusId) => new ListCursor(taskId, StatusId: statusId),
        ListSort.DueDate when DateTimeOffset.TryParseExact(value, "O", CultureInfo.InvariantCulture, DateTimeStyles.None, out var due) => new ListCursor(taskId, DueDate: due),
        ListSort.Label when value.Length >= 1 && value.Length <= LabelName.MaxLength => new ListCursor(taskId, FirstLabelName: value),
        _ => null,
    };
}
