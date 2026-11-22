using System.Collections.Concurrent;
using System.Text.Json;
using Yello.Contracts;
using Yello.Contracts.Tasks;
using Yello.Merge;
using Yello.Tests.Shared;

namespace Yello.Tests.Client;

/// <summary>
/// A Task's description on the fake: the read answers <c>{ text, state, version }</c> and the change route takes a batch of
/// encoded changes and answers <c>{ refused, version, missing }</c>, as the Host answers them. A case seeds a Task's text,
/// holds the next batch, refuses every change in it, or fails it, and reads every batch the page sent.
/// </summary>
/// <remarks>
/// <para>
/// <b>Backed by a replica of the real strategy per Task</b>, seeded from <see cref="Descriptions"/> the first time the Task
/// is read or written, so the page's editor merges against a true replica and every batch carries real changes. After each
/// batch the Task's entry in <see cref="Descriptions"/> is the replica's text.
/// </para>
/// <para>
/// <b>The pipeline's answers first</b>: an unlisted Space is the boundary <c>404</c>; a Viewer's batch is the <c>403</c>
/// naming the capability, and a Viewer still reads.
/// </para>
/// </remarks>
internal sealed partial class FakeYelloApi
{
    private const string DescriptionSegment = "description";

    private const string ChangesSegment = "changes";

    private readonly ConcurrentDictionary<Guid, ITextReplica> _descriptionReplicas = new();

    private readonly Lock _descriptionLock = new();

    private TaskEditHold? _descriptionHold;

    private TaskEditHold? _lastDescriptionHold;

    /// <summary>
    /// Each Task's description text as the server holds it, seeded by a case before the page opens the panel.
    /// </summary>
    public ConcurrentDictionary<Guid, string> Descriptions { get; } = new();

    /// <summary>
    /// When set, every change in the next batch is refused (answered <c>200</c> with each listed), so the page withdraws them.
    /// </summary>
    public bool RefuseEveryChangeInTheNextBatch { get; set; }

    /// <summary>
    /// When set, the next batch is answered <c>503</c>, so the page keeps its changes for the next flush.
    /// </summary>
    public bool FailTheNextBatch { get; set; }

    /// <summary>
    /// The armed hold on the next batch, if any.
    /// </summary>
    public TaskEditHold? DescriptionHold => _descriptionHold;

    /// <summary>
    /// Every change batch the page sent, in order.
    /// </summary>
    public IReadOnlyList<FakeRequest> DescriptionBatches =>
        [.. _requests.Where(request => request.Method == "POST" && request.Path.EndsWith("/description/changes", StringComparison.Ordinal))];

    /// <summary>
    /// Every description read the page sent, in order.
    /// </summary>
    public IReadOnlyList<FakeRequest> DescriptionReads =>
        [.. _requests.Where(request => request.Method == "GET" && request.Path.EndsWith("/description", StringComparison.Ordinal))];

    /// <summary>
    /// The encoded changes a batch carried, as the base64 strings the page sent.
    /// </summary>
    /// <param name="batch">A change batch.</param>
    /// <returns>The changes.</returns>
    public static IReadOnlyList<string> ChangesIn(FakeRequest batch)
    {
        ArgumentNullException.ThrowIfNull(batch);

        using var document = JsonDocument.Parse(batch.Body ?? "{}");

        return document.RootElement.TryGetProperty("changes", out var changes) && changes.ValueKind == JsonValueKind.Array
            ? [.. changes.EnumerateArray().Select(change => change.GetString() ?? string.Empty)]
            : [];
    }

    /// <summary>
    /// Holds the <b>next</b> change batch until it is released.
    /// </summary>
    /// <returns>A task that completes when the held batch has arrived and is waiting.</returns>
    public Task HoldDescriptionBatchAsync()
    {
        var hold = new TaskEditHold();

        _descriptionHold = hold;
        _lastDescriptionHold = hold;

        return hold.Arrived.Task;
    }

    /// <summary>
    /// Lets the held batch go; it is then answered as any other.
    /// </summary>
    public void ReleaseDescriptionBatch() => _lastDescriptionHold?.Released.TrySetResult(TaskEditOutcome.Admitted);

    /// <summary>
    /// The description routes' answers: <c>null</c> when the request is neither.
    /// </summary>
    private async Task<(int Status, string Body)?> DescriptionAsync(FakeRequest request)
    {
        if (!TryDescription(request.Path, out var spaceId, out var taskId, out var changes))
        {
            return null;
        }

        if (!Switcher.Any(space => space.Id == spaceId))
        {
            return (404, Problem(ProblemTypes.NotAvailable));
        }

        if (!changes)
        {
            return request.Method == "GET" ? Read(taskId) : null;
        }

        if (request.Method != "POST")
        {
            return null;
        }

        if (Roles.TryGetValue(spaceId, out var role) && role == "viewer")
        {
            return (403, JsonSerializer.Serialize(new ProblemResponse(ProblemTypes.InsufficientRole, Errors: null, Capability: TaskDescriptionWire.EditCapabilityCode), JsonOptions));
        }

        if (Interlocked.Exchange(ref _descriptionHold, null) is { } hold)
        {
            hold.Arrived.TrySetResult();
            await hold.Released.Task.ConfigureAwait(false);
        }

        if (FailTheNextBatch)
        {
            FailTheNextBatch = false;

            return (503, Problem(ProblemTypes.ServiceUnavailable));
        }

        return Apply(taskId, request.Body ?? "{}");
    }


    private (int Status, string Body) Read(Guid taskId)
    {
        lock (_descriptionLock)
        {
            var replica = ReplicaOf(taskId);

            return (200, JsonSerializer.Serialize(new TaskDescriptionResponse(replica.Text, replica.EncodeState(), replica.Version.Encoded), JsonOptions));
        }
    }

    private (int Status, string Body) Apply(Guid taskId, string body)
    {
        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;
        var sent = root.GetProperty("changes").EnumerateArray().Select(change => change.GetBytesFromBase64()).ToList();
        var version = TextMerge.CreateStrategy().DecodeVersion(root.GetProperty("version").GetBytesFromBase64());

        lock (_descriptionLock)
        {
            var replica = ReplicaOf(taskId);
            var refused = new List<int>();
            var refuseAll = RefuseEveryChangeInTheNextBatch;

            RefuseEveryChangeInTheNextBatch = false;

            for (var position = 0; position < sent.Count; position++)
            {
                if (refuseAll
                    || TextMerge.CreateStrategy().DecodeChange(sent[position]) is not { Admission: ChangeAdmission.Admitted, Value: { } change }
                    || replica.Apply(change) != ChangeAdmission.Admitted)
                {
                    refused.Add(position);
                }
            }

            Descriptions[taskId] = replica.Text;

            IReadOnlyList<ReadOnlyMemory<byte>> missing = version.Value is { } client
                ? [.. replica.ChangesMissingFrom(client).Select(change => change.Encoded)]
                : [];

            return (200, JsonSerializer.Serialize(new TaskDescriptionChangesResponse(refused, replica.Version.Encoded, missing), JsonOptions));
        }
    }

    /// <summary>
    /// The Task's server replica, made the first time from the seeded text under the Task's own identity, as the Host's is.
    /// </summary>
    private ITextReplica ReplicaOf(Guid taskId) =>
        _descriptionReplicas.GetOrAdd(taskId, id =>
        {
            var replica = TextMerge.CreateStrategy().CreateReplica(new ReplicaId(id));

            if (Descriptions.TryGetValue(id, out var text) && text.Length > 0)
            {
                replica.Insert(0, text);
            }

            return replica;
        });

    private static bool TryDescription(string path, out Guid spaceId, out Guid taskId, out bool changes)
    {
        spaceId = Guid.Empty;
        taskId = Guid.Empty;
        changes = false;

        // /api/v1/spaces/{spaceId}/tasks/{taskId}/description[/changes]
        var segments = path.Trim('/').Split('/');

        if (segments is not ["api", "v1", "spaces", var space, "tasks", var task, DescriptionSegment, .. var rest]
            || rest is not ([] or [ChangesSegment])
            || !Guid.TryParse(space, out spaceId)
            || !Guid.TryParse(task, out taskId))
        {
            return false;
        }

        changes = rest.Length == 1;

        return true;
    }
}
