using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Yello.Domain;
using Yello.Domain.Tasks;
using Yello.Merge;

namespace Yello.Infrastructure.Persistence;

/// <summary>
/// The merge adapter: a Task's description log and its projection, inside the request's Space-scoped unit of work. It
/// admits each change through <see cref="ITextMergeStrategy"/>, appends the newly admitted ones, compacts, and writes the
/// projection, all in the transaction that appends.
/// </summary>
/// <remarks>
/// <para>
/// <b>The Task's row lock comes before the log is read.</b> Two appends to one description queue on it, so the second
/// rebuilds from a log that already holds the first's rows, takes the next sequence after them and projects text that
/// holds both. Reading the log first would let the second project without the first's text, or collide on the sequence.
/// Plain visibility read first, then <c>UPDLOCK, INDEX(1)</c> on the clustered key and no <c>HOLDLOCK</c>, as every Task
/// row locker here takes it (<see cref="TaskStore"/>); the append takes no Project lock.
/// </para>
/// <para>
/// <b>One writer of the projection</b>: <see cref="ProjectAsync"/>, from the replica the rows were applied to, never from
/// a client's text and never by re-reading the column. The architecture suite holds it to being the only one.
/// </para>
/// <para>
/// <b>Compaction never removes a change row.</b> When the change rows after the latest snapshot reach the threshold, the
/// write that reaches it appends a snapshot of the replica after them and deletes the superseded snapshot, which has no
/// author. Every change keeps its author, name and time, so per-author counts and timestamps survive by construction.
/// </para>
/// </remarks>
/// <param name="dbContext">The request's unit of work, already narrowed to the active Space.</param>
/// <param name="identifiers">Produces each row's identity.</param>
/// <param name="clock">Stamps each row.</param>
/// <param name="merge">The merge port the Host registers.</param>
/// <param name="options">The compaction threshold.</param>
internal sealed class TaskDescriptionStore(
    YelloDbContext dbContext,
    IIdentifierGenerator identifiers,
    TimeProvider clock,
    ITextMergeStrategy merge,
    IOptions<TaskDescriptionOptions> options) : ITaskDescriptionLog
{
    /// <inheritdoc />
    public Task<DescriptionAppend> AppendAsync(Guid spaceId, Guid taskId, Guid authorAccountId, DescriptionBatch batch, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(batch);

        return DescriptionReplica.VersionOf(merge, batch.ClientVersion) is { } client
            ? AppendAsync(new Append(spaceId, taskId, authorAccountId, batch, client), cancellationToken)
            : Task.FromResult(DescriptionAppend.InvalidVersion);
    }

    /// <inheritdoc />
    public async Task<DescriptionRead?> ReadAsync(Guid taskId, CancellationToken cancellationToken)
    {
        var task = await dbContext.Tasks
            .AsNoTracking()
            .Where(candidate => candidate.Id == taskId)
            .Select(candidate => new { candidate.Description })
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (task is null)
        {
            return null;
        }

        var replica = (await RebuildAsync(taskId, cancellationToken).ConfigureAwait(false)).Replica;

        return new DescriptionRead(task.Description, replica.EncodeState(), replica.Version.Encoded);
    }

    private async Task<DescriptionAppend> AppendAsync(Append append, CancellationToken cancellationToken)
    {
        if (!await LockAsync(append.TaskId, cancellationToken).ConfigureAwait(false))
        {
            return DescriptionAppend.NotFound;
        }

        var log = await RebuildAsync(append.TaskId, cancellationToken).ConfigureAwait(false);
        var (refused, appended) = DescriptionReplica.Admit(merge, log.Replica, append.Batch);

        if (appended.Count > 0)
        {
            await AppendRowsAsync(append, appended, log, cancellationToken).ConfigureAwait(false);
            await ProjectAsync(append.TaskId, log.Replica.Text, cancellationToken).ConfigureAwait(false);
        }

        return new DescriptionAppend(
            DescriptionAppendOutcome.Applied,
            refused,
            log.Replica.Version.Encoded,
            [.. log.Replica.ChangesMissingFrom(append.Client).Select(change => change.Encoded)]);
    }

    /// <summary>
    /// Whether the Task is visible, and if so its row, taken under the lock every Task row locker takes.
    /// </summary>
    /// <remarks>
    /// A plain read first: a locking point read on a missing key locks the next key, which can be another Space's.
    /// </remarks>
    private async Task<bool> LockAsync(Guid taskId, CancellationToken cancellationToken)
    {
        var visible = await dbContext.Tasks
            .AsNoTracking()
            .AnyAsync(task => task.Id == taskId, cancellationToken)
            .ConfigureAwait(false);

        if (!visible)
        {
            return false;
        }

        var locked = await dbContext.Tasks
            .FromSql($"SELECT * FROM dbo.[Task] WITH (UPDLOCK, INDEX(1)) WHERE Id = {taskId}")
            .AsNoTracking()
            .Select(task => new { task.Id })
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        return locked is not null;
    }

    /// <summary>
    /// The server replica rebuilt from the latest snapshot and the change rows after it, never the whole log.
    /// </summary>
    private async Task<RebuiltLog> RebuildAsync(Guid taskId, CancellationToken cancellationToken)
    {
        var rows = dbContext.Set<TaskDescriptionChange>().AsNoTracking().Where(row => row.TaskId == taskId);

        var snapshot = await rows
            .Where(row => row.Kind == TaskDescriptionChangeKind.Snapshot)
            .OrderByDescending(row => row.Sequence)
            .Select(row => new { row.Id, row.Sequence, row.Payload })
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        var after = snapshot?.Sequence ?? 0;

        var changes = await rows
            .Where(row => row.Kind == TaskDescriptionChangeKind.Change && row.Sequence > after)
            .OrderBy(row => row.Sequence)
            .Select(row => new { row.Sequence, row.Payload })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var replica = DescriptionReplica.Rebuild(merge, taskId, snapshot?.Payload, changes.Select(row => row.Payload));

        return new RebuiltLog(replica, snapshot?.Id, changes.Count, changes.Count > 0 ? changes[^1].Sequence : after);
    }

    /// <summary>
    /// One row per newly admitted change, in batch order after the log's last row; then, when the change rows since the
    /// latest snapshot reach the threshold, a snapshot of the replica after them in place of the superseded one.
    /// </summary>
    private async Task AppendRowsAsync(Append append, List<byte[]> appended, RebuiltLog log, CancellationToken cancellationToken)
    {
        var authorName = await AuthorNameAsync(append.AuthorAccountId, cancellationToken).ConfigureAwait(false);
        var now = clock.GetUtcNow();
        var sequence = log.LastSequence;
        var rows = dbContext.Set<TaskDescriptionChange>();

        foreach (var payload in appended)
        {
            sequence++;
            rows.Add(new TaskDescriptionChange
            {
                Id = identifiers.Next(),
                SpaceId = append.SpaceId,
                TaskId = append.TaskId,
                Sequence = sequence,
                Kind = TaskDescriptionChangeKind.Change,
                Payload = payload,
                AuthorAccountId = append.AuthorAccountId,
                AuthorName = authorName,
                RecordedAt = now,
            });
        }

        if (log.ChangesSinceSnapshot + appended.Count >= options.Value.CompactionThreshold)
        {
            rows.Add(new TaskDescriptionChange
            {
                Id = identifiers.Next(),
                SpaceId = append.SpaceId,
                TaskId = append.TaskId,
                Sequence = sequence + 1,
                Kind = TaskDescriptionChangeKind.Snapshot,
                Payload = log.Replica.EncodeState().ToArray(),
                RecordedAt = now,
            });

            if (log.SnapshotId is { } superseded)
            {
                await rows.Where(row => row.Id == superseded).ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// The author's display name as it stands now, captured onto the rows so attribution never looks it up live.
    /// </summary>
    private Task<string> AuthorNameAsync(Guid accountId, CancellationToken cancellationToken) =>
        dbContext.Accounts
            .AsNoTracking()
            .Where(account => account.Id == accountId)
            .Select(account => account.DisplayName)
            .SingleAsync(cancellationToken);

    /// <summary>
    /// The projector: writes the Task's description from the replica the rows were applied to, in the appending
    /// transaction, so a read after an admitted write is never stale.
    /// </summary>
    private Task<int> ProjectAsync(Guid taskId, string text, CancellationToken cancellationToken) =>
        dbContext.Tasks
            .Where(task => task.Id == taskId)
            .ExecuteUpdateAsync(setters => setters.SetProperty(task => task.Description, text), cancellationToken);

    private sealed record Append(Guid SpaceId, Guid TaskId, Guid AuthorAccountId, DescriptionBatch Batch, ReplicaVersion Client);

    private sealed record RebuiltLog(ITextReplica Replica, Guid? SnapshotId, int ChangesSinceSnapshot, long LastSequence);
}
