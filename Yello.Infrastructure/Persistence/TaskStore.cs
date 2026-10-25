using Microsoft.EntityFrameworkCore;
using Yello.Domain;
using Yello.Domain.Tasks;

namespace Yello.Infrastructure.Persistence;

/// <summary>
/// Creates Tasks inside the request's Space-scoped unit of work. Story 2.2.
/// </summary>
/// <remarks>
/// <para>
/// <b>No <c>Where</c> on <c>SpaceId</c>, anywhere.</b> Both isolation layers are already narrowed to the
/// request's Space (see <see cref="StatusDefinitionReader"/>), so a Project in another Space is invisible
/// here exactly as one that never existed is - and the creation answers <c>null</c> for it before any
/// insert, so the composite foreign key is never the thing that refuses it (a <c>500</c>).
/// </para>
/// <para>
/// <b>The position key is the column's last plus one</b> (<see cref="PositionKey.After"/>). The read of
/// the last key and the insert cannot race another creation in the same Project: the pipeline counted
/// the Project's Tasks under <c>UPDLOCK, HOLDLOCK</c> before this ran (<see cref="BoundCounter"/>), so a
/// second creation waits at its count until this one commits.
/// </para>
/// <para>
/// <b>Saves its own effect</b>, as every store does, so the idempotency record is the only thing the
/// pipeline flushes after it (<see cref="IdempotencyStore"/>).
/// </para>
/// </remarks>
/// <param name="dbContext">The request's unit of work.</param>
/// <param name="identifiers">Produces the Task's identity (AR-34).</param>
/// <param name="clock">Stamps the creation instant.</param>
internal sealed class TaskStore(YelloDbContext dbContext, IIdentifierGenerator identifiers, TimeProvider clock)
    : ITaskStore
{
    /// <inheritdoc />
    public async Task<TaskItem?> CreateAsync(
        Guid spaceId,
        Guid projectId,
        Guid statusId,
        string title,
        CancellationToken cancellationToken)
    {
        var visible = await dbContext.Projects
            .AnyAsync(project => project.Id == projectId, cancellationToken)
            .ConfigureAwait(false);

        if (!visible)
        {
            return null;
        }

        var last = await dbContext.Tasks
            .Where(task => task.ProjectId == projectId && task.StatusId == statusId)
            .OrderByDescending(task => task.PositionKey)
            .Select(task => task.PositionKey)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        var task = new TaskItem
        {
            Id = identifiers.Next(),
            SpaceId = spaceId,
            ProjectId = projectId,
            StatusId = statusId,
            Title = title,
            PositionKey = PositionKey.After(last),
            CreatedAt = clock.GetUtcNow(),
        };

        dbContext.Tasks.Add(task);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return task;
    }
}
