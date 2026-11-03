using System.Buffers.Text;
using System.Globalization;
using System.Text;

namespace Yello.Application.Memberships.ListMemberships;

/// <summary>
/// The Membership list's cursor on the wire: an opaque, URL-safe string naming the last served row's
/// <c>(CreatedAt, Id)</c>. Story 4.4, AC1, Q8 (a) - the <see cref="Lists.ListCursorText"/> style.
/// </summary>
/// <remarks>
/// <b>Base64url of a fixed, versioned text form</b>: <c>m1</c>, the instant in round-trip form, and the id - newline
/// separated. <see cref="TryRead"/> refuses anything the route did not issue, by its text alone, so the refusal is a
/// statement about the request (<c>400 invalid-list-cursor</c>). A well-formed cursor whose Membership has gone is read like
/// any other: the page after it is the page after its pair.
/// </remarks>
public static class MembershipCursorText
{
    private const int Longest = 256;

    private const string Version = "m1";

    private const char Separator = '\n';

    private const int Parts = 3;

    private const int VersionPart = 0;

    private const int InstantPart = 1;

    private const int IdPart = 2;

    private static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    /// <summary>
    /// The cursor after <paramref name="cursor"/>, as the route issues it.
    /// </summary>
    /// <param name="cursor">The last served row's position.</param>
    /// <returns>The cursor text.</returns>
    public static string Write(MembershipCursor cursor)
    {
        ArgumentNullException.ThrowIfNull(cursor);

        var text = string.Join(
            Separator,
            Version,
            cursor.CreatedAt.ToString("O", CultureInfo.InvariantCulture),
            cursor.Id.ToString("N"));

        return Base64Url.EncodeToString(Encoding.UTF8.GetBytes(text));
    }

    /// <summary>
    /// Reads a cursor sent with a page request - <c>false</c> for anything the route did not issue.
    /// </summary>
    /// <param name="sent">The cursor as sent.</param>
    /// <param name="cursor">The position, when it reads.</param>
    /// <returns>Whether it reads.</returns>
    public static bool TryRead(string sent, out MembershipCursor? cursor)
    {
        ArgumentNullException.ThrowIfNull(sent);

        cursor = null;

        if (Decode(sent) is not { } text)
        {
            return false;
        }

        var parts = text.Split(Separator);

        if (parts.Length != Parts
            || !string.Equals(parts[VersionPart], Version, StringComparison.Ordinal)
            || !DateTimeOffset.TryParseExact(parts[InstantPart], "O", CultureInfo.InvariantCulture, DateTimeStyles.None, out var createdAt)
            || !Guid.TryParseExact(parts[IdPart], "N", out var id))
        {
            return false;
        }

        cursor = new MembershipCursor(createdAt, id);

        return true;
    }

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
}
