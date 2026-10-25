using Microsoft.EntityFrameworkCore;
using Yello.Domain;
using Yello.Domain.Tasks;

namespace Yello.Infrastructure.Persistence;

/// <summary>
/// Creates, reads and edits Tasks inside the request's Space-scoped unit of work. Stories 2.2 and 2.3.
/// </summary>
/// <remarks>
/// <para>
/// <b>No <c>Where</c> on <c>SpaceId</c>, anywhere.</b> Both isolation layers are already narrowed to the
/// request's Space (see <see cref="StatusDefinitionReader"/>), so a Project or Task in another Space is
/// invisible here exactly as one that never existed is - and the creation answers <c>null</c> for it before
/// any insert, so the composite foreign key is never the thing that refuses it (a <c>500</c>).
/// </para>
/// <para>
/// <b>The position key is the column's last plus one</b> (<see cref="PositionKey.After"/>), and <b>both
/// writers that place a key take the same lock first</b>: the Project's Tasks under
/// <c>UPDLOCK, HOLDLOCK</c>. A creation takes it in the pipeline (the Tasks-per-Project bound,
/// <see cref="BoundCounter"/>); a Status change takes it here, in <see cref="EditAsync"/>, with the same range
/// over the same <c>SpaceId</c>-leading index. So an edit and a creation into one column, or two edits, queue
/// at that range, and the second reads the first's key as the last (story 2.3 Task 6, AC9).
/// </para>
/// <para>
/// <b>Why the Project's range and not only the column's.</b> Both scan the index in one order, so every key
/// placer takes its key-range locks in the same order and none can hold one column while waiting on another
/// that the first holds - a column-only lock taken by the edit would interleave with the creation's
/// Project-wide scan and could deadlock.
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

    /// <inheritdoc />
    public Task<TaskItem?> FindAsync(Guid taskId, CancellationToken cancellationToken) =>
        dbContext.Tasks
            .AsNoTracking()
            .SingleOrDefaultAsync(task => task.Id == taskId, cancellationToken);

    /// <inheritdoc />
    public Task<TaskItem?> EditAsync(Guid taskId, TaskEdit edit, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(edit);

        return ApplyAsync(taskId, edit, cancellationToken);
    }

    private async Task<TaskItem?> ApplyAsync(Guid taskId, TaskEdit edit, CancellationToken cancellationToken)
    {
        var task = await dbContext.Tasks
            .SingleOrDefaultAsync(candidate => candidate.Id == taskId, cancellationToken)
            .ConfigureAwait(false);

        if (task is null)
        {
            return null;
        }

        // Any Status named takes the lock, the Task's current one included: the Status read before the lock may
        // be one that a move still uncommitted is about to replace, and this edit, coming after it, must win.
        if (edit.StatusId is { } statusId)
        {
            var last = await LastKeyUnderTheProjectLockAsync(task.ProjectId, statusId, cancellationToken).ConfigureAwait(false);

            // Re-read under the lock: another edit may have moved this Task while this one waited.
            var entry = dbContext.Entry(task);
            await entry.ReloadAsync(cancellationToken).ConfigureAwait(false);

            if (entry.State == EntityState.Detached)
            {
                return null;
            }

            if (task.StatusId != statusId)
            {
                task.StatusId = statusId;
                task.PositionKey = PositionKey.After(last);
            }
        }

        if (edit.Title is { } title)
        {
            task.Title = title;
        }

        if (edit.ChangesDueDate)
        {
            task.DueDate = edit.DueDate;
        }

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return task;
    }

    /// <summary>
    /// Takes the Project's Task range under <c>UPDLOCK, HOLDLOCK</c> - the range a creation's bound count
    /// takes - and reads the destination column's last key in the same statement. The row-level security
    /// policy narrows it to the request's Space; raw SQL lives only in this ring, and every value is a
    /// parameter.
    /// </summary>
    private Task<string?> LastKeyUnderTheProjectLockAsync(Guid projectId, Guid statusId, CancellationToken cancellationToken) =>
        dbContext.Database
            .SqlQuery<string?>($"SELECT MAX(CASE WHEN StatusId = {statusId} THEN PositionKey END) AS [Value] FROM dbo.[Task] WITH (UPDLOCK, HOLDLOCK) WHERE ProjectId = {projectId}")
            .SingleAsync(cancellationToken);
}
