namespace Yello.Client.Spaces;

/// <summary>
/// The Status name rule, mirrored for the client: trimmed, 1-100 characters.
/// </summary>
/// <remarks>
/// <b>A courtesy, never the decision.</b> The editor checks a name before sending it so an empty one is said at once; the
/// server checks it again - uniqueness in the Project's set included - and its answer is what stands. The client cannot
/// reference the Domain's <c>StatusName</c>.
/// </remarks>
public static class StatusNameRule
{
    private const int Longest = 100;

    /// <summary>
    /// Whether a submitted name is one once trimmed.
    /// </summary>
    /// <param name="submitted">The name as typed.</param>
    /// <returns><c>true</c> when it is 1-100 characters after trimming.</returns>
    public static bool IsStatusName(string submitted) =>
        (submitted?.Trim().Length ?? 0) is >= 1 and <= Longest;
}
