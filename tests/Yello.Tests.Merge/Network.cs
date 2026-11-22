using Xunit;
using Yello.Merge;

namespace Yello.Tests.Merge;

/// <summary>
/// The order in which a replica's inbox is applied.
/// </summary>
internal enum Delivery
{
    AsSent,
    Reversed,
    Shuffled,
}

/// <summary>
/// An in-memory, deterministic network of replicas: per-replica inboxes the case drains in the order it chooses, isolation
/// and reconnection through the "has" exchange alone, and injected duplicates.
/// </summary>
/// <remarks>
/// <para>
/// <b>It can deliver a change before the change it depends on.</b> A shuffled or reversed inbox does exactly that, and a
/// FIFO-only harness would never show whether the candidate holds such a change until its predecessor arrives.
/// </para>
/// <para>
/// A change made by an isolated replica reaches nobody, and a change sent to an isolated replica is lost. Reconnection
/// recovers both through <see cref="ITextReplica.ChangesMissingFrom"/>, never by copying text.
/// </para>
/// </remarks>
internal sealed class Network
{
    private readonly ITextReplica[] _replicas;
    private readonly List<TextChange>[] _inboxes;
    private readonly bool[] _isolated;
    private readonly List<(int From, TextChange Change)> _sent = [];

    public Network(ITextMergeStrategy strategy, int size)
    {
        _replicas = [.. Enumerable.Range(0, size).Select(index => strategy.CreateReplica(IdOf(index)))];
        _inboxes = [.. Enumerable.Range(0, size).Select(_ => new List<TextChange>())];
        _isolated = new bool[size];
    }

    public Ledger Ledger { get; } = new();

    public int Size => _replicas.Length;

    /// <summary>
    /// Every change any connected replica sent, in the order sent.
    /// </summary>
    public IReadOnlyList<(int From, TextChange Change)> Sent => _sent;

    public ITextReplica this[int index] => _replicas[index];

    public IReadOnlyList<string> Texts => [.. _replicas.Select(replica => replica.Text)];

    public static ReplicaId IdOf(int index) => new(new Guid(index + 1, 0x7a11, 0x0001, 0, 0, 0, 0, 0, 0, 0, 0));

    public bool IsIsolated(int replica) => _isolated[replica];

    public int Pending(int replica) => _inboxes[replica].Count;

    /// <summary>
    /// <paramref name="text"/> typed by replica 0 into an empty document and delivered everywhere, as a shared starting
    /// point that is not part of the concurrency under test.
    /// </summary>
    public void Seed(string text)
    {
        Insert(0, 0, text);
        DeliverAll(Delivery.AsSent);
    }

    public TextChange Insert(int replica, int index, string text)
    {
        var change = _replicas[replica].Insert(index, text);

        Ledger.RecordInsert(replica, text);
        Broadcast(replica, change);

        return change;
    }

    public TextChange Delete(int replica, int index, int length)
    {
        var removed = _replicas[replica].Text.Substring(index, length);
        var change = _replicas[replica].Delete(index, length);

        Ledger.RecordDelete(removed);
        Broadcast(replica, change);

        return change;
    }

    /// <summary>
    /// A selection overwritten by typing: a delete, then an insert at the same place.
    /// </summary>
    public void Replace(int replica, int index, int length, string text)
    {
        Delete(replica, index, length);
        Insert(replica, index, text);
    }

    /// <summary>
    /// Puts a change into a replica's inbox again, as a retried frame would.
    /// </summary>
    public void Enqueue(int replica, TextChange change) => _inboxes[replica].Add(change);

    public void Deliver(int replica, Delivery order, int seed = 0)
    {
        var inbox = _inboxes[replica];
        var batch = order switch
        {
            Delivery.AsSent => inbox.ToList(),
            Delivery.Reversed => Enumerable.Reverse(inbox).ToList(),
            _ => Shuffle(inbox, new Random(seed)),
        };

        inbox.Clear();

        foreach (var change in batch)
        {
            Admit(_replicas[replica], change);
        }
    }

    /// <summary>
    /// Applies up to <paramref name="count"/> changes from a replica's inbox, chosen at random, leaving the rest queued.
    /// </summary>
    public void DeliverSome(int replica, int count, Random random)
    {
        var inbox = _inboxes[replica];

        for (var i = 0; i < count && inbox.Count > 0; i++)
        {
            var pick = random.Next(inbox.Count);
            var change = inbox[pick];

            inbox.RemoveAt(pick);
            Admit(_replicas[replica], change);
        }
    }

    public void DeliverAll(Delivery order)
    {
        for (var replica = 0; replica < Size; replica++)
        {
            Deliver(replica, order);
        }
    }

    /// <summary>
    /// Every inbox drained, each replica in a different random order.
    /// </summary>
    public void DeliverEachInItsOwnOrder(int seed)
    {
        for (var replica = 0; replica < Size; replica++)
        {
            Deliver(replica, Delivery.Shuffled, seed + replica);
        }
    }

    public void Isolate(int replica) => _isolated[replica] = true;

    /// <summary>
    /// Ends a replica's isolation and runs the reconnection exchange with every connected replica.
    /// </summary>
    public void Reconnect(int replica)
    {
        _isolated[replica] = false;

        for (var other = 0; other < Size; other++)
        {
            if (other != replica && !_isolated[other])
            {
                Exchange(_replicas[replica], _replicas[other]);
            }
        }
    }

    /// <summary>
    /// The reconnection exchange: each side sends its "has", and receives what it lacks.
    /// </summary>
    public static void Exchange(ITextReplica a, ITextReplica b)
    {
        var aLacks = b.ChangesMissingFrom(a.Version);
        var bLacks = a.ChangesMissingFrom(b.Version);

        foreach (var change in aLacks)
        {
            Admit(a, change);
        }

        foreach (var change in bLacks)
        {
            Admit(b, change);
        }
    }

    public static void Admit(ITextReplica replica, TextChange change) =>
        Assert.Equal(ChangeAdmission.Admitted, replica.Apply(change));

    public void AssertConverged()
    {
        var texts = Texts;

        Assert.True(
            texts.Distinct(StringComparer.Ordinal).Count() == 1,
            "The replicas hold different texts after every change was delivered:" + Environment.NewLine
            + string.Join(Environment.NewLine, texts.Select((text, index) => $"  replica {index}: \"{Abbreviate(text)}\"")));
    }

    /// <summary>
    /// Asserts the ledger holds at every replica: inserts not deleted present once, deletes absent.
    /// </summary>
    public void AssertNothingLostOrResurrected(Func<int, bool> insertedBy, string what)
    {
        for (var replica = 0; replica < Size; replica++)
        {
            var problems = Ledger.Problems(_replicas[replica].Text, insertedBy);

            Assert.True(
                problems.Count == 0,
                $"{what}, at replica {replica}: {problems.Count} problem(s), first ones:" + Environment.NewLine
                + string.Join(Environment.NewLine, problems.Take(5)));
        }
    }

    private static List<TextChange> Shuffle(List<TextChange> inbox, Random random)
    {
        var items = inbox.ToArray();

        random.Shuffle(items);

        return [.. items];
    }

    private static string Abbreviate(string text) => text.Length <= 120 ? text : string.Concat(text.AsSpan(0, 120), "...");

    private void Broadcast(int from, TextChange change)
    {
        if (_isolated[from])
        {
            return;
        }

        _sent.Add((from, change));

        for (var to = 0; to < Size; to++)
        {
            if (to != from && !_isolated[to])
            {
                _inboxes[to].Add(change);
            }
        }
    }
}
