using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Yello.Contracts.Tasks;
using Yello.Merge;

namespace Yello.Client.Components;

/// <summary>
/// One opened panel's editing session of a Task's description: a local replica of the merge, the changes not yet admitted,
/// and the exchange with the server that sends them and takes back what the editor lacks.
/// </summary>
/// <remarks>
/// <para>
/// <b>Changes, never text.</b> Each input becomes the replica's inserts and deletes (<see cref="TextDiff"/>), so the
/// server only ever receives merge changes. The replica's identity is fresh for every session, never reused, so two panels
/// never issue the same change identity.
/// </para>
/// <para>
/// <b>One batch at a time.</b> A batch that fails is kept, in order, for the next send; a batch the server answers has its
/// refused changes withdrawn, latest first, and the changes this replica lacks applied.
/// </para>
/// </remarks>
/// <param name="http">The API client.</param>
/// <param name="merge">The merge port.</param>
/// <param name="spaceId">The Space the Task is in.</param>
/// <param name="taskId">The Task.</param>
internal sealed class DescriptionSession(HttpClient http, ITextMergeStrategy merge, Guid spaceId, Guid taskId)
{
    private const int MostChangesPerBatch = 256;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly List<TextChange> _pending = [];

    /// <summary>
    /// What the outcome of one send was.
    /// </summary>
    internal enum Sent
    {
        /// <summary>
        /// Nothing was pending, or a batch was already out.
        /// </summary>
        Nothing,

        /// <summary>
        /// The server answered, or refused the batch outright; the replica now reflects the answer.
        /// </summary>
        Settled,

        /// <summary>
        /// No answer: the batch is kept for the next send.
        /// </summary>
        Failed,
    }

    /// <summary>
    /// Gets the local replica, or <c>null</c> until the description has been read.
    /// </summary>
    internal ITextReplica? Replica { get; private set; }

    /// <summary>
    /// Gets the projection's text as the read answered it, or <c>null</c> when it could not be read.
    /// </summary>
    internal string? ReadText { get; private set; }

    /// <summary>
    /// Gets a value indicating whether changes are waiting to be admitted.
    /// </summary>
    internal bool HasPending => _pending.Count > 0;

    /// <summary>
    /// Gets a value indicating whether a batch is with the server.
    /// </summary>
    internal bool InFlight { get; private set; }

    /// <summary>
    /// Reads the description and bootstraps the replica from its state under a fresh identity.
    /// </summary>
    /// <param name="cancellationToken">The Space's token.</param>
    /// <returns>A task.</returns>
    internal async Task LoadAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var response = await http.GetAsync(new Uri(TaskRoutes.DescriptionOf(spaceId, taskId), UriKind.Relative), cancellationToken);

            if (!response.IsSuccessStatusCode
                || await response.Content.ReadFromJsonAsync<TaskDescriptionResponse>(JsonOptions, cancellationToken) is not { } read)
            {
                return;
            }

            ReadText = read.Text;

            if (merge.LoadReplica(new ReplicaId(Guid.NewGuid()), read.State.Span) is { Admission: ChangeAdmission.Admitted, Value: { } replica })
            {
                Replica = replica;
            }
        }
        catch (Exception exception) when (exception is OperationCanceledException or HttpRequestException or JsonException)
        {
            // Unread: the region shows its heading and nothing to edit; the disconnection message is not this panel's.
        }
    }

    /// <summary>
    /// Records an edit of the field as changes to send.
    /// </summary>
    /// <param name="before">The text the field held, which is the replica's.</param>
    /// <param name="after">The text it holds now.</param>
    internal void Record(string before, string after)
    {
        if (Replica is not null)
        {
            _pending.AddRange(TextDiff.Record(Replica, before, after));
        }
    }

    /// <summary>
    /// Sends the next batch of pending changes and settles the answer.
    /// </summary>
    /// <param name="cancellationToken">The Space's token.</param>
    /// <returns>What the send came to.</returns>
    internal async Task<Sent> SendAsync(CancellationToken cancellationToken)
    {
        if (Replica is null || InFlight || _pending.Count == 0)
        {
            return Sent.Nothing;
        }

        InFlight = true;

        var batch = _pending.Take(MostChangesPerBatch).ToList();
        var answer = await PostAsync(batch, Replica.Version, cancellationToken);

        InFlight = false;

        if (answer is null)
        {
            return Sent.Failed;
        }

        Settle(Replica, batch, answer);

        return Sent.Settled;
    }

    /// <summary>
    /// The server's answer, or <c>null</c> for no answer. A refusal of the whole batch (the Role changed, the Task went) is
    /// read as every change refused: the server will never take them.
    /// </summary>
    private async Task<TaskDescriptionChangesResponse?> PostAsync(List<TextChange> batch, ReplicaVersion version, CancellationToken cancellationToken)
    {
        var request = new TaskDescriptionChangesRequest([.. batch.Select(change => change.Encoded)], version.Encoded);

        try
        {
            using var response = await http.PostAsJsonAsync(new Uri(TaskRoutes.DescriptionChangesOf(spaceId, taskId), UriKind.Relative), request, JsonOptions, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<TaskDescriptionChangesResponse>(JsonOptions, cancellationToken);
            }

            return response.StatusCode >= HttpStatusCode.InternalServerError
                ? null
                : new TaskDescriptionChangesResponse([.. Enumerable.Range(0, batch.Count)], ReadOnlyMemory<byte>.Empty, []);
        }
        catch (Exception exception) when (exception is OperationCanceledException or HttpRequestException or JsonException)
        {
            return null;
        }
    }

    private void Settle(ITextReplica replica, List<TextChange> batch, TaskDescriptionChangesResponse answer)
    {
        var refused = answer.Refused.Where(position => position >= 0 && position < batch.Count).Select(position => batch[position]).ToHashSet();

        _pending.RemoveRange(0, batch.Count);
        Withdraw(replica, batch, refused);

        foreach (var bytes in answer.Missing)
        {
            if (merge.DecodeChange(bytes.Span) is { Admission: ChangeAdmission.Admitted, Value: { } change })
            {
                replica.Apply(change);
            }
        }
    }

    /// <summary>
    /// Takes back the refused changes, latest first, as the port requires. One that a later, still unsent change builds on
    /// takes those later changes back with it: they were typed on text the server never accepted.
    /// </summary>
    private void Withdraw(ITextReplica replica, List<TextChange> batch, HashSet<TextChange> refused)
    {
        foreach (var change in Enumerable.Reverse(batch).Where(refused.Contains))
        {
            try
            {
                replica.Withdraw(change.Id);
            }
            catch (InvalidOperationException)
            {
                foreach (var later in Enumerable.Reverse(_pending))
                {
                    TryWithdraw(replica, later);
                }

                _pending.Clear();
                TryWithdraw(replica, change);
            }
        }
    }

    private static void TryWithdraw(ITextReplica replica, TextChange change)
    {
        try
        {
            replica.Withdraw(change.Id);
        }
        catch (InvalidOperationException)
        {
            // Another change still builds on it; it stays, and the replica converges with the server through the merge.
        }
    }
}
