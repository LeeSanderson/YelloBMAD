using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Yello.Domain.Tasks;

namespace Yello.Infrastructure.Persistence;

/// <summary>
/// Moves every Task of one Project's Status to the end of another column - of the same Project for a Project's own Status
/// removal and a Space-level one, of another Project for a bulk move between Projects.
/// </summary>
/// <remarks>
/// <para>
/// <b>One <c>UPDATE</c>, however many Tasks.</b> The keys are computed here from the destination column's last key, read after
/// the caller took the Projects' Tasks locks, each <see cref="PositionKey.After"/> the one before in the source column's own
/// order, and joined in from <c>OPENJSON</c>: a statement per Task would be thousands of round trips inside the lock. No
/// destination sibling's key changes. The <c>WHERE</c> keeps the source Project and Status, so a removal never touches
/// another Project's Tasks, and <c>ProjectId</c> is written with the source's own id when the two are the same.
/// </para>
/// <para>
/// <b>No row lock of its own.</b> A Labels-only edit takes a Task's row without a Project's lock; this waits for that row,
/// and the Labels edit never waits on a Project's lock, so the two cannot cycle. That holds for a bulk move too: it takes
/// both Projects' locks before this statement, and the Labels edit takes neither.
/// </para>
/// </remarks>
internal static class TaskStatusMigration
{
    /// <summary>
    /// Moves the Tasks to the end of the destination column, in their source order.
    /// </summary>
    /// <param name="dbContext">The request's unit of work, holding the Tasks lock of both Projects.</param>
    /// <param name="sourceProjectId">The Project the Tasks are in.</param>
    /// <param name="fromStatusId">The Status they leave.</param>
    /// <param name="destinationProjectId">The Project they go to; the source itself for a Status removal.</param>
    /// <param name="toStatusId">The Status they go to, one the destination's set holds.</param>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    /// <returns>How many Tasks the statement wrote.</returns>
    internal static async Task<int> MigrateAsync(
        YelloDbContext dbContext,
        Guid sourceProjectId,
        Guid fromStatusId,
        Guid destinationProjectId,
        Guid toStatusId,
        CancellationToken cancellationToken)
    {
        var occupants = await dbContext.Tasks
            .AsNoTracking()
            .Where(task => task.ProjectId == sourceProjectId && task.StatusId == fromStatusId)
            .OrderBy(task => task.PositionKey)
            .Select(task => task.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (occupants.Count == 0)
        {
            return 0;
        }

        var last = await dbContext.Database
            .SqlQuery<string?>($"SELECT MAX(PositionKey) AS [Value] FROM dbo.[Task] WHERE ProjectId = {destinationProjectId} AND StatusId = {toStatusId}")
            .SingleAsync(cancellationToken)
            .ConfigureAwait(false);

        var placements = new List<MigratedPlacement>(occupants.Count);

        foreach (var id in occupants)
        {
            last = PositionKey.After(last);
            placements.Add(new MigratedPlacement(id, last));
        }

        var map = JsonSerializer.Serialize(placements);

        return await dbContext.Database
            .ExecuteSqlAsync(
                $"""
                UPDATE t SET ProjectId = {destinationProjectId}, StatusId = {toStatusId}, PositionKey = m.[Key]
                FROM dbo.[Task] AS t
                JOIN OPENJSON({map}) WITH (Id uniqueidentifier '$.Id', [Key] varchar(128) '$.Key') AS m ON m.Id = t.Id
                WHERE t.ProjectId = {sourceProjectId} AND t.StatusId = {fromStatusId}
                """,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private sealed record MigratedPlacement(Guid Id, string Key);
}
