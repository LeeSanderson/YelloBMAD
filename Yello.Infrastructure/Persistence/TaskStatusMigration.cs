using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Yello.Domain.Tasks;

namespace Yello.Infrastructure.Persistence;

/// <summary>
/// Moves every Task of one Project from one Status to another, for a Project's own removal and a Space-level one alike.
/// </summary>
/// <remarks>
/// <para>
/// <b>One <c>UPDATE</c>, however many Tasks.</b> The keys are computed here from the destination's last key, read after
/// the caller took the Project's Tasks lock, each <see cref="PositionKey.After"/> the one before in the source column's own
/// order, and joined in from <c>OPENJSON</c>: a statement per Task would be thousands of round trips inside the lock. No
/// destination sibling's key changes.
/// </para>
/// <para>
/// <b>No row lock of its own.</b> A Labels-only edit takes a Task's row without the Project's lock; this waits for that
/// row, and the Labels edit never waits on the Project's lock, so the two cannot cycle.
/// </para>
/// </remarks>
internal static class TaskStatusMigration
{
    /// <summary>
    /// Moves the Tasks to the end of the destination column, in their source order.
    /// </summary>
    /// <param name="dbContext">The request's unit of work, holding the Project's Tasks lock.</param>
    /// <param name="projectId">The Project.</param>
    /// <param name="fromStatusId">The Status they leave.</param>
    /// <param name="toStatusId">The Status they go to.</param>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    /// <returns>A task.</returns>
    internal static async Task MigrateAsync(YelloDbContext dbContext, Guid projectId, Guid fromStatusId, Guid toStatusId, CancellationToken cancellationToken)
    {
        var occupants = await dbContext.Tasks
            .AsNoTracking()
            .Where(task => task.ProjectId == projectId && task.StatusId == fromStatusId)
            .OrderBy(task => task.PositionKey)
            .Select(task => task.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (occupants.Count == 0)
        {
            return;
        }

        var last = await dbContext.Database
            .SqlQuery<string?>($"SELECT MAX(PositionKey) AS [Value] FROM dbo.[Task] WHERE ProjectId = {projectId} AND StatusId = {toStatusId}")
            .SingleAsync(cancellationToken)
            .ConfigureAwait(false);

        var placements = new List<MigratedPlacement>(occupants.Count);

        foreach (var id in occupants)
        {
            last = PositionKey.After(last);
            placements.Add(new MigratedPlacement(id, last));
        }

        var map = JsonSerializer.Serialize(placements);

        await dbContext.Database
            .ExecuteSqlAsync(
                $"""
                UPDATE t SET StatusId = {toStatusId}, PositionKey = m.[Key]
                FROM dbo.[Task] AS t
                JOIN OPENJSON({map}) WITH (Id uniqueidentifier '$.Id', [Key] varchar(128) '$.Key') AS m ON m.Id = t.Id
                WHERE t.ProjectId = {projectId} AND t.StatusId = {fromStatusId}
                """,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private sealed record MigratedPlacement(Guid Id, string Key);
}
