namespace Yello.Client.Spaces;

/// <summary>
/// The Task title rule, mirrored for the client: trimmed, 1-200 characters. Story 2.3, Task 9 (open question
/// 9 (a)).
/// </summary>
/// <remarks>
/// <para>
/// <b>A courtesy, never the decision.</b> The Task detail panel checks a title before sending it so an empty
/// one is said at once; the server checks it again and its answer is what stands (AD-11).
/// </para>
/// <para>
/// <b>Held to the Domain's <c>TaskTitle</c> by a gate</b>:
/// <c>RefusalBoundaryGateTests.The_clients_Task_title_predicate_agrees_with_the_Domain_rule</c>. The client
/// cannot reference the Domain ring (AD-21).
/// </para>
/// </remarks>
public static class TaskTitleRule
{
    private const int Longest = 200;

    /// <summary>
    /// Whether a submitted title is one once trimmed.
    /// </summary>
    /// <param name="submitted">The title as typed.</param>
    /// <returns><c>true</c> when it is 1-200 characters after trimming.</returns>
    public static bool IsTaskTitle(string submitted) =>
        (submitted?.Trim().Length ?? 0) is >= 1 and <= Longest;
}
