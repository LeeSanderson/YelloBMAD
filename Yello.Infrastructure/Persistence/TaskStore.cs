using System.Data.SqlTypes;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Yello.Domain;
using Yello.Domain.Tasks;

namespace Yello.Infrastructure.Persistence;

/// <summary>
/// Creates, reads, edits, moves and deletes Tasks, and the Labels applied to them, inside the request's Space-scoped unit
/// of work. Stories 2.2, 2.3, 2.4, 2.5 and 2.6.
/// </summary>
/// <remarks>
/// <para>
/// <b>No <c>Where</c> on <c>SpaceId</c>, anywhere.</b> Both isolation layers are already narrowed to the
/// request's Space (see <see cref="StatusDefinitionReader"/>), so a Project or Task in another Space is
/// invisible here exactly as one that never existed is - and the creation answers <c>null</c> for it before
/// any insert, so the composite foreign key is never the thing that refuses it (a <c>500</c>).
/// </para>
/// <para>
/// <b>A creation's and a Status edit's position key is the column's last plus one</b> (<see cref="PositionKey.After"/>),
/// and <b>every writer that places a key takes the same lock first</b>: the Project's Tasks under
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
/// <b>Applying Labels locks them first</b> (story 2.4, AC10): the submitted Labels are read under
/// <c>UPDLOCK, HOLDLOCK</c> on the clustered key that a deletion locks too (<see cref="LabelStore"/> says why it is
/// named) - raw SQL composed onto the <c>Label</c> set, so the query filter and the security
/// policy both still narrow it - and a set with any Label missing is refused before a <c>TaskLabel</c> row is
/// written, so the composite foreign key never answers it as a <c>500</c>. A deletion that committed first is then
/// simply missing; one that comes second waits on the Label lock and then finds the application
/// (<see cref="LabelStore"/>). <b>Lock order</b>: an edit takes the Project's Task range (a Status change only), then
/// the Task's own row, then the Labels in the server's id order, then reads the Task's <c>TaskLabel</c> rows; a
/// deletion takes the Label, then its <c>TaskLabel</c> range. Label before TaskLabel in both, and a deletion never
/// takes a Task lock, so the two cannot wait on each other in a cycle - measured by the story's race cases, which
/// hold each side's lock and wait on the other. The Task row lock serialises two Labels edits of one Task, so the
/// whole-set replacement stays one request's set.
/// </para>
/// <para>
/// <b>A deletion takes the edit's locks in the edit's order</b> (story 2.5, open question 5): the Project's Task range
/// first - the statement a creation's bound count and a Status edit take, so the same index and the same range - then the
/// Task's own row on its clustered key (<c>INDEX(1)</c>, the Labels edit's Task lock), then the delete; the database's
/// <c>TaskLabel</c> cascade removes the applications inside that statement. Range before row, as every key placer: a
/// Status edit holds the range and then writes the row, so a deletion that locked the row first would hold what the edit
/// needs while waiting for what it has. A Label deletion takes the Label and then its <c>TaskLabel</c> range and never a
/// Task lock, so it queues behind the cascade's removed rows and never cycles. A deletion writes no position key and
/// renumbers no sibling (AD-15). Measured by story 2.5's RACE-T1..T5, which hold each side and wait on the other.
/// </para>
/// <para>
/// <b>A move is the fourth key placer and takes the same locks in the same order</b> (story 2.6, AC11): the Project's
/// Task range in the creation count's statement shape, then the Task's own row on its clustered key (<c>INDEX(1)</c>),
/// re-read under it - gone, or no longer in the Project whose range is held, is not found - then, only now, the
/// neighbour and the first key after it in the destination column, the moved Task left out. Its key is placed strictly
/// between the two (<see cref="PositionKey.Between"/>) and only its own row is written; no sibling's key ever changes
/// (AD-15). A move to where the Task already is writes nothing but still took the locks. Measured by story 2.6's
/// RACE-M1..M8, which hold each side and wait on the other: reading the neighbours before the range, or the range
/// without its hints, is caught 3/3 by the read race (RACE-M2); the row's <c>INDEX(1)</c> and its
/// <c>UPDLOCK, HOLDLOCK</c> are each masked 0/3 - the range already serialises every key placer - and kept as every
/// Task writer's row lock.
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
    /// <summary>
    /// SQL Server's ordering of <c>uniqueidentifier</c>, which is not <see cref="Guid"/>'s own.
    /// </summary>
    private static readonly IComparer<Guid> ServerGuidOrder =
        Comparer<Guid>.Create((first, second) => new SqlGuid(first).CompareTo(new SqlGuid(second)));

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

    /// <inheritdoc />
    public Task<bool> ReplaceLabelsAsync(Guid taskId, IReadOnlyList<Guid> labelIds, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(labelIds);

        return ReplaceAsync(taskId, labelIds, cancellationToken);
    }

    /// <inheritdoc />
    public Task<IReadOnlyDictionary<Guid, IReadOnlyList<Guid>>> LabelIdsOfAsync(IReadOnlyCollection<Guid> taskIds, CancellationToken cancellationToken) =>
        TaskLabelReads.LabelIdsOfAsync(dbContext, taskIds, cancellationToken);

    private async Task<bool> ReplaceAsync(Guid taskId, IReadOnlyList<Guid> labelIds, CancellationToken cancellationToken)
    {
        // The Task's own row first, under UPDLOCK, HOLDLOCK on its clustered key: two edits of one Task's Labels queue
        // here, so the second reads the first's applications as current and the set stored is exactly one request's -
        // never a merge of two, and never a second DELETE of a row the first already removed (a 500).
        var owner = await dbContext.Tasks
            .FromSql($"SELECT * FROM dbo.[Task] WITH (UPDLOCK, HOLDLOCK, INDEX(1)) WHERE Id = {taskId}")
            .AsNoTracking()
            .Select(task => new { task.SpaceId })
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (owner is null)
        {
            return false;
        }

        var space = owner.SpaceId;

        if (labelIds.Count > 0)
        {
            // In the server's uniqueidentifier order, so two edits naming the same Labels in different orders lock
            // them in one order and cannot deadlock on each other.
            var submitted = JsonSerializer.Serialize(labelIds.Order(ServerGuidOrder));

            var visible = await dbContext.Labels
                .FromSql($"SELECT * FROM dbo.[Label] WITH (UPDLOCK, HOLDLOCK, INDEX(1)) WHERE Id IN (SELECT CAST([value] AS uniqueidentifier) FROM OPENJSON({submitted}))")
                .AsNoTracking()
                .CountAsync(cancellationToken)
                .ConfigureAwait(false);

            if (visible != labelIds.Count)
            {
                return false;
            }
        }

        var current = await dbContext.Set<TaskLabel>()
            .Where(application => application.TaskId == taskId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        dbContext.Set<TaskLabel>().RemoveRange(current.Where(application => !labelIds.Contains(application.LabelId)));

        var now = clock.GetUtcNow();

        dbContext.Set<TaskLabel>().AddRange(labelIds
            .Where(labelId => !current.Exists(application => application.LabelId == labelId))
            .Select(labelId => new TaskLabel { SpaceId = space, TaskId = taskId, LabelId = labelId, AppliedAt = now }));

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return true;
    }

    /// <inheritdoc />
    public async Task<TaskDeletion> DeleteAsync(Guid taskId, CancellationToken cancellationToken)
    {
        var owner = await dbContext.Tasks
            .AsNoTracking()
            .Where(task => task.Id == taskId)
            .Select(task => new { task.ProjectId })
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (owner is null)
        {
            return TaskDeletion.NotFound;
        }

        var project = owner.ProjectId;

        // The Project's Task range, in the statement shape a creation's bound count takes, so every key placer meets on it.
        await TakeTheProjectRangeAsync(project, cancellationToken).ConfigureAwait(false);

        // The Task's own row, on the clustered key every other Task locker names. Gone by now (deleted while this waited
        // on the range) is not found - never Deleted, never a 500.
        var locked = await dbContext.Tasks
            .FromSql($"SELECT * FROM dbo.[Task] WITH (UPDLOCK, HOLDLOCK, INDEX(1)) WHERE Id = {taskId}")
            .AsNoTracking()
            .CountAsync(cancellationToken)
            .ConfigureAwait(false);

        if (locked == 0)
        {
            return TaskDeletion.NotFound;
        }

        var deleted = await dbContext.Tasks
            .Where(task => task.Id == taskId)
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);

        return deleted == 0 ? TaskDeletion.NotFound : TaskDeletion.Deleted;
    }

    /// <inheritdoc />
    public async Task<TaskMove> MoveAsync(Guid taskId, Guid statusId, Guid? afterTaskId, CancellationToken cancellationToken)
    {
        var owner = await dbContext.Tasks
            .AsNoTracking()
            .Where(task => task.Id == taskId)
            .Select(task => new { task.ProjectId })
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (owner is null)
        {
            return TaskMove.NotFound;
        }

        var project = owner.ProjectId;

        // Range before row, as every key placer: the Project's Task range in the creation count's statement shape.
        await TakeTheProjectRangeAsync(project, cancellationToken).ConfigureAwait(false);

        // The Task's own row, on the clustered key every other Task locker names, read again under it: gone by now - or
        // no longer in the Project whose range this holds - is not found, never Moved.
        var task = await dbContext.Tasks
            .FromSql($"SELECT * FROM dbo.[Task] WITH (UPDLOCK, HOLDLOCK, INDEX(1)) WHERE Id = {taskId}")
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (task is null || task.ProjectId != project)
        {
            return TaskMove.NotFound;
        }

        // The neighbours, read only now that the range is held: nothing in this Project can be placed, moved or
        // deleted until this commits, so the gap read here is the gap written into.
        var (available, lower) = await NeighbourKeyAsync(project, statusId, taskId, afterTaskId, cancellationToken).ConfigureAwait(false);

        if (!available)
        {
            return TaskMove.PositionUnavailable;
        }

        var upper = await SuccessorAsync(project, statusId, taskId, lower, cancellationToken).ConfigureAwait(false);

        // Already there: nothing to write, but the locks were still taken (story 2.6, open question 18).
        if (task.StatusId == statusId && SortsBetween(lower, task.PositionKey, upper))
        {
            return TaskMove.Moved(task);
        }

        if (Placed(lower, upper) is not { } key)
        {
            // No key fits the gap: refused, never a renumber of the siblings (AD-15, open question 10).
            return TaskMove.PositionUnavailable;
        }

        task.StatusId = statusId;
        task.PositionKey = key;

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return TaskMove.Moved(task);
    }

    private static bool SortsBetween(string? lower, string key, string? upper) =>
        (lower is null || string.CompareOrdinal(lower, key) < 0) && (upper is null || string.CompareOrdinal(key, upper) < 0);

    private static string? Placed(string? lower, string? upper)
    {
        try
        {
            return PositionKey.Between(lower, upper, RandomNumberGenerator.GetInt32);
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    /// <summary>
    /// The key the moved Task lands after: <c>null</c> for the top of the column, or the neighbour's key when it is a Task
    /// of the destination column of the same Project and not the moved Task itself; otherwise not available.
    /// </summary>
    private async Task<(bool Available, string? Key)> NeighbourKeyAsync(
        Guid projectId,
        Guid statusId,
        Guid movedTaskId,
        Guid? afterTaskId,
        CancellationToken cancellationToken)
    {
        if (afterTaskId is not { } after)
        {
            return (true, null);
        }

        var neighbour = await dbContext.Tasks
            .AsNoTracking()
            .Where(candidate => candidate.Id == after && candidate.Id != movedTaskId && candidate.ProjectId == projectId && candidate.StatusId == statusId)
            .Select(candidate => candidate.PositionKey)
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        return (neighbour is not null, neighbour);
    }

    /// <summary>
    /// The first key in one column of a Project after <paramref name="lower"/> - or the column's first key when it is
    /// <c>null</c> - leaving out the moved Task itself. Compared in the column's own collation (BIN2).
    /// </summary>
    private Task<string?> SuccessorAsync(Guid projectId, Guid statusId, Guid movedTaskId, string? lower, CancellationToken cancellationToken)
    {
        var column = dbContext.Tasks
            .AsNoTracking()
            .Where(task => task.ProjectId == projectId && task.StatusId == statusId && task.Id != movedTaskId);

        if (lower is not null)
        {
            column = column.Where(task => string.Compare(task.PositionKey, lower) > 0);
        }

        return column
            .OrderBy(task => task.PositionKey)
            .Select(task => task.PositionKey)
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <summary>
    /// Takes the Project's Task range under <c>UPDLOCK, HOLDLOCK</c> in the statement a creation's bound count takes
    /// (<see cref="BoundCounter"/>): the same index and the same range, so a deletion and a move meet a creation and a
    /// Status edit on it in one order.
    /// </summary>
    private Task TakeTheProjectRangeAsync(Guid projectId, CancellationToken cancellationToken) =>
        dbContext.Database
            .SqlQuery<int>($"SELECT COUNT(*) AS [Value] FROM dbo.[Task] WITH (UPDLOCK, HOLDLOCK) WHERE ProjectId = {projectId}")
            .SingleAsync(cancellationToken);

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
