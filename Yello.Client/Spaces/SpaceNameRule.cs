namespace Yello.Client.Spaces;

/// <summary>
/// The Space name rule, mirrored for the client: trimmed, 1-200 characters. Story 3.1, open question 6 (a).
/// </summary>
/// <remarks>
/// <para>
/// <b>A courtesy, never the decision.</b> The create page and the rename form check a name before sending it so an
/// empty one is said at once; the server checks it again, and its <c>400 invalid-space-name</c> is what stands.
/// </para>
/// <para>
/// <b>Held to the Domain's <c>SpaceName</c> by a gate</b>:
/// <c>RefusalBoundaryGateTests.The_clients_Space_name_predicate_agrees_with_the_Domain_rule</c>. The client cannot
/// reference the Domain ring.
/// </para>
/// </remarks>
public static class SpaceNameRule
{
    private const int Longest = 200;

    /// <summary>
    /// Whether a submitted name is one once trimmed.
    /// </summary>
    /// <param name="submitted">The name as typed.</param>
    /// <returns><c>true</c> when it is 1-200 characters after trimming.</returns>
    public static bool IsSpaceName(string submitted) =>
        (submitted?.Trim().Length ?? 0) is >= 1 and <= Longest;
}
