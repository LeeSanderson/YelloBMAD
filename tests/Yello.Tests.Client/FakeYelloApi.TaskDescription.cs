using System.Collections.Concurrent;
using System.Text.Json;
using Yello.Tests.Shared;

namespace Yello.Tests.Client;

/// <summary>
/// A Task's description on the fake: the read answers <c>{ text, state, version }</c> and the change route takes a batch of
/// encoded changes and answers <c>{ refused, version, missing }</c>, as the Host is to answer them. A case seeds a Task's text,
/// holds the next batch, refuses every change in it, or fails it, and reads every batch the page sent.
/// </summary>
/// <remarks>
/// <para>
/// <b>Not yet routed.</b> Answering needs a server replica of the real strategy, and this project reaches
/// <c>Yello.Merge</c> only once Infrastructure references it. The develop phase adds <c>DescriptionAsync</c> beside the other
/// partials' hooks in <c>FakeYelloApi.SpaceDeletion.cs</c>, backs <see cref="Descriptions"/> with a replica per Task built
/// from the seeded text, and answers the controls below. Until then this partial is inert: no route reaches it, so every
/// other case is served exactly what it was.
/// </para>
/// <para>
/// The routes are <see cref="TaskDescriptionWire"/>'s stand-ins.
/// </para>
/// </remarks>
internal sealed partial class FakeYelloApi
{
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
    public TaskEditHold? DescriptionHold { get; private set; }

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

        DescriptionHold = hold;
        _lastDescriptionHold = hold;

        return hold.Arrived.Task;
    }

    /// <summary>
    /// Lets the held batch go; it is then answered as any other.
    /// </summary>
    public void ReleaseDescriptionBatch() => _lastDescriptionHold?.Released.TrySetResult(TaskEditOutcome.Admitted);
}
