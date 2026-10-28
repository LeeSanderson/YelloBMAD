namespace Yello.Client.Components;

/// <summary>
/// What the Board's keyboard needs of the Board it is attached to (story 2.8): a key pressed on one of the composite's stops,
/// and where focus moved.
/// </summary>
public interface IBoardKeysHost
{
    /// <summary>
    /// A key pressed on a stop: a card's title (<paramref name="taskId"/>) or a column itself (<paramref name="statusId"/>).
    /// </summary>
    /// <param name="key">The key, the arrows already mapped to logical directions.</param>
    /// <param name="taskId">The card's Task, or <c>null</c>.</param>
    /// <param name="statusId">The column's Status, or <c>null</c>.</param>
    /// <returns>A task that completes once a drop's move has been answered.</returns>
    Task KeyAsync(BoardKey key, Guid? taskId, Guid? statusId);

    /// <summary>
    /// Focus moved to a stop - or left the Board, when both are <c>null</c>.
    /// </summary>
    /// <param name="taskId">The card's Task, or <c>null</c>.</param>
    /// <param name="statusId">The column's Status, or <c>null</c>.</param>
    void Focused(Guid? taskId, Guid? statusId);
}
