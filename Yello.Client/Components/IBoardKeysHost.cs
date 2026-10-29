namespace Yello.Client.Components;

/// <summary>
/// What the Board's keyboard needs of the Board it is attached to (story 2.8): a key pressed on one of the composite's stops,
/// and where focus moved. Story 2.11 adds the Status pager's keys and the viewport crossing 768px.
/// </summary>
public interface IBoardKeysHost
{
    /// <summary>
    /// A key pressed on a stop: a card's title (<paramref name="taskId"/>) or a column itself (<paramref name="statusId"/>).
    /// </summary>
    /// <param name="key">The key, the arrows already mapped to logical directions.</param>
    /// <param name="narrow">Whether the viewport was below 768px when the key was pressed (story 2.11).</param>
    /// <param name="taskId">The card's Task, or <c>null</c>.</param>
    /// <param name="statusId">The column's Status, or <c>null</c>.</param>
    /// <returns>A task that completes once a drop's move has been answered.</returns>
    Task KeyAsync(BoardKey key, bool narrow, Guid? taskId, Guid? statusId);

    /// <summary>
    /// Focus moved to a stop - or left the Board, when both are <c>null</c>.
    /// </summary>
    /// <param name="taskId">The card's Task, or <c>null</c>.</param>
    /// <param name="statusId">The column's Status, or <c>null</c>.</param>
    void Focused(Guid? taskId, Guid? statusId);

    /// <summary>
    /// A key on one of the Status pager's tabs (story 2.11, AC4, AC9).
    /// </summary>
    /// <param name="key">The key, the arrows already mapped to logical directions.</param>
    /// <param name="statusId">The tab's Status.</param>
    void PagerKey(BoardKey key, Guid statusId);

    /// <summary>
    /// The viewport is below 768px, or no longer is (story 2.11, AC14).
    /// </summary>
    /// <param name="narrow">Whether the viewport is below 768px.</param>
    /// <param name="fromPager">Whether focus was on one of the Status pager's tabs.</param>
    /// <param name="focusedStatusId">The Status of the column holding focus, or <c>null</c> when no column holds it.</param>
    void Narrowed(bool narrow, bool fromPager, Guid? focusedStatusId);
}
