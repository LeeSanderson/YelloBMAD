namespace Yello.Domain.Tasks;

/// <summary>
/// A Task's description log in the Space the current unit of work is scoped to: admits a batch of encoded merge changes,
/// appends the newly admitted ones and re-projects the text, and reads the projection with the state an editor starts from.
/// </summary>
/// <remarks>
/// <para>
/// <b>Bytes in, bytes and outcomes out.</b> The merge sits behind this port, in Infrastructure, so no merge type reaches
/// Domain or Application. The server never takes text from a client: it admits or refuses each change.
/// </para>
/// <para>
/// <b>Never asked about another Space</b>, for the reason <see cref="ITaskStore"/> gives.
/// </para>
/// </remarks>
public interface ITaskDescriptionLog
{
    /// <summary>
    /// Locks the Task's row, rebuilds its description from the log, decodes and applies each change on its own, appends one
    /// row per change not held before, and writes the projection, all in the unit of work's transaction.
    /// </summary>
    /// <param name="spaceId">The resolved Space, stamped on each row.</param>
    /// <param name="taskId">The Task, from the route.</param>
    /// <param name="authorAccountId">The Account writing, recorded on each appended row with its name as it stands now.</param>
    /// <param name="batch">The encoded changes and the client replica's encoded version.</param>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    /// <returns>What the batch came to.</returns>
    Task<DescriptionAppend> AppendAsync(
        Guid spaceId,
        Guid taskId,
        Guid authorAccountId,
        DescriptionBatch batch,
        CancellationToken cancellationToken);

    /// <summary>
    /// Reads the projection as a column, and the encoded state an editor bootstraps from.
    /// </summary>
    /// <param name="taskId">The Task, from the route.</param>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    /// <returns>The description, or <c>null</c> when no Task with that id is visible.</returns>
    Task<DescriptionRead?> ReadAsync(Guid taskId, CancellationToken cancellationToken);
}

/// <summary>
/// A batch of description changes as a client sent it.
/// </summary>
/// <param name="Changes">The encoded changes, in the order the client made them.</param>
/// <param name="ClientVersion">The client replica's encoded version.</param>
public sealed record DescriptionBatch(IReadOnlyList<ReadOnlyMemory<byte>> Changes, ReadOnlyMemory<byte> ClientVersion);

/// <summary>
/// What a batch of description changes came to.
/// </summary>
/// <param name="Outcome">Whether it was applied.</param>
/// <param name="Refused">The batch positions, from zero, of the changes refused; each is withdrawn by the client.</param>
/// <param name="Version">The server's encoded version after the batch.</param>
/// <param name="Missing">The encoded changes the client's version lacks.</param>
public sealed record DescriptionAppend(
    DescriptionAppendOutcome Outcome,
    IReadOnlyList<int> Refused,
    ReadOnlyMemory<byte> Version,
    IReadOnlyList<ReadOnlyMemory<byte>> Missing)
{
    /// <summary>
    /// No Task with the id is visible in the Space.
    /// </summary>
    public static DescriptionAppend NotFound { get; } = new(DescriptionAppendOutcome.NotFound, [], ReadOnlyMemory<byte>.Empty, []);

    /// <summary>
    /// The client's version is not one the merge can read, so nothing was applied.
    /// </summary>
    public static DescriptionAppend InvalidVersion { get; } = new(DescriptionAppendOutcome.InvalidVersion, [], ReadOnlyMemory<byte>.Empty, []);
}

/// <summary>
/// Whether a batch of description changes was applied.
/// </summary>
public enum DescriptionAppendOutcome
{
    /// <summary>
    /// Each change was admitted or refused on its own, and the admitted ones are in the log.
    /// </summary>
    Applied,

    /// <summary>
    /// No Task with the id is visible in the Space.
    /// </summary>
    NotFound,

    /// <summary>
    /// The client's version could not be read.
    /// </summary>
    InvalidVersion,
}

/// <summary>
/// A Task's description as read.
/// </summary>
/// <param name="Text">The projection, read as a column.</param>
/// <param name="State">The encoded replica state the log rebuilds to.</param>
/// <param name="Version">The server's encoded version.</param>
public sealed record DescriptionRead(string Text, ReadOnlyMemory<byte> State, ReadOnlyMemory<byte> Version);
