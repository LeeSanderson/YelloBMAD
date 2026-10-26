namespace Yello.Client.Spaces;

/// <summary>
/// The Label name rule, mirrored for the client: trimmed, 1-50 characters. Story 2.4, open question 2.
/// </summary>
/// <remarks>
/// <para>
/// <b>A courtesy, never the decision.</b> The define form checks a name before sending it so an empty one is said at
/// once; the server checks it again - uniqueness in the Space included - and its answer is what stands.
/// </para>
/// <para>
/// <b>Held to the Domain's <c>LabelName</c> by a gate</b>:
/// <c>RefusalBoundaryGateTests.The_clients_Label_name_predicate_agrees_with_the_Domain_rule</c>. The client cannot
/// reference the Domain ring.
/// </para>
/// </remarks>
public static class LabelNameRule
{
    private const int Longest = 50;

    /// <summary>
    /// Whether a submitted name is one once trimmed.
    /// </summary>
    /// <param name="submitted">The name as typed.</param>
    /// <returns><c>true</c> when it is 1-50 characters after trimming.</returns>
    public static bool IsLabelName(string submitted) =>
        (submitted?.Trim().Length ?? 0) is >= 1 and <= Longest;
}
