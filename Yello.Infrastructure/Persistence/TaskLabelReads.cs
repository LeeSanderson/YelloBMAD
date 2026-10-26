using Microsoft.EntityFrameworkCore;

namespace Yello.Infrastructure.Persistence;

/// <summary>
/// The one read of which Labels some Tasks carry, shared by the Task read, the Task edit and the Board. Story 2.4.
/// </summary>
/// <remarks>
/// <para>
/// <b>One statement for the whole set of Tasks</b>, never one per Task: <c>TaskLabel</c> joined to <c>Label</c> for
/// the Task ids given, so the Board's page costs one more statement whatever it holds (<c>deferred-work.md:493</c>).
/// </para>
/// <para>
/// <b>Each Task's list is by Label name, then id</b> (open question 12), in the database's collation, so a card's
/// first three chips are the same on every read and the client never re-sorts.
/// </para>
/// <para>
/// <b>No <c>Where</c> on <c>SpaceId</c></b>, for the reason <see cref="StatusDefinitionReader"/> gives.
/// </para>
/// </remarks>
internal static class TaskLabelReads
{
    /// <summary>
    /// The Labels applied to each of <paramref name="taskIds"/>.
    /// </summary>
    /// <param name="dbContext">The request's unit of work.</param>
    /// <param name="taskIds">The Tasks.</param>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    /// <returns>The Label ids per Task id, each by Label name then id; a Task carrying none is absent.</returns>
    internal static async Task<IReadOnlyDictionary<Guid, IReadOnlyList<Guid>>> LabelIdsOfAsync(
        YelloDbContext dbContext,
        IReadOnlyCollection<Guid> taskIds,
        CancellationToken cancellationToken)
    {
        if (taskIds.Count == 0)
        {
            return new Dictionary<Guid, IReadOnlyList<Guid>>();
        }

        var rows = await dbContext.Set<TaskLabel>()
            .AsNoTracking()
            .Where(application => taskIds.Contains(application.TaskId))
            .Join(dbContext.Labels, application => application.LabelId, label => label.Id, (application, label) => new { application.TaskId, label.Name, label.Id })
            .OrderBy(row => row.TaskId)
            .ThenBy(row => row.Name)
            .ThenBy(row => row.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return rows
            .GroupBy(row => row.TaskId)
            .ToDictionary(group => group.Key, IReadOnlyList<Guid> (group) => [.. group.Select(row => row.Id)]);
    }
}
