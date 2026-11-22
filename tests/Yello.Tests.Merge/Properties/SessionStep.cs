using System.Globalization;

namespace Yello.Tests.Merge.Properties;

/// <summary>
/// One step of a generated editing session. Positions are fractions of the replica's text at the time the step runs, so
/// a trace with steps removed still replays: that is what lets a failure shrink to its minimal trace.
/// </summary>
internal abstract record SessionStep
{
    public abstract void Run(Network network, Glyphs glyphs);

    protected static int ScalarAt(string text, double position, bool endAllowed)
    {
        var scalars = Utf16.ScalarCount(text);
        var span = endAllowed ? scalars + 1 : scalars;

        return Math.Min(int.CreateTruncating(position * span), Math.Max(span - 1, 0));
    }

    protected static string Format(double value) => value.ToString("0.###", CultureInfo.InvariantCulture);
}

internal sealed record InsertStep(int Replica, double Position, int Length, bool Wide) : SessionStep
{
    public override void Run(Network network, Glyphs glyphs)
    {
        var text = network[Replica].Text;

        network.Insert(Replica, Utf16.IndexOf(text, ScalarAt(text, Position, endAllowed: true)), glyphs.Take(Length, Wide));
    }

    public override string ToString() => $"replica {Replica} inserts {Length}{(Wide ? " wide" : string.Empty)} at {Format(Position)}";
}

internal sealed record DeleteStep(int Replica, double Position, int Length) : SessionStep
{
    public override void Run(Network network, Glyphs glyphs)
    {
        var text = network[Replica].Text;

        if (text.Length == 0)
        {
            return;
        }

        var index = Utf16.IndexOf(text, ScalarAt(text, Position, endAllowed: false));

        network.Delete(Replica, index, Utf16.LengthOf(text, index, Length));
    }

    public override string ToString() => $"replica {Replica} deletes {Length} at {Format(Position)}";
}

internal sealed record ReplaceStep(int Replica, double Position, int DeleteLength, int InsertLength) : SessionStep
{
    public override void Run(Network network, Glyphs glyphs)
    {
        var text = network[Replica].Text;

        if (text.Length == 0)
        {
            return;
        }

        var index = Utf16.IndexOf(text, ScalarAt(text, Position, endAllowed: false));

        network.Replace(Replica, index, Utf16.LengthOf(text, index, DeleteLength), glyphs.Take(InsertLength));
    }

    public override string ToString() => $"replica {Replica} replaces {DeleteLength} with {InsertLength} at {Format(Position)}";
}

/// <summary>
/// Some of a replica's inbox applied, in a random order: later changes before earlier ones, and before the changes they
/// depend on.
/// </summary>
internal sealed record DeliverStep(int Replica, int Count, int OrderSeed) : SessionStep
{
    public override void Run(Network network, Glyphs glyphs) =>
        network.DeliverSome(Replica, Count, new Random(OrderSeed));

    public override string ToString() => $"replica {Replica} receives {Count} (order {OrderSeed})";
}

/// <summary>
/// A change another replica sent is queued at <see cref="Replica"/> a second time, as a retried frame would be.
/// </summary>
internal sealed record DuplicateStep(int Replica, double Which) : SessionStep
{
    public override void Run(Network network, Glyphs glyphs)
    {
        var candidates = network.Sent.Where(sent => sent.From != Replica).ToList();

        if (candidates.Count > 0)
        {
            network.Enqueue(Replica, candidates[Math.Min(int.CreateTruncating(Which * candidates.Count), candidates.Count - 1)].Change);
        }
    }

    public override string ToString() => $"replica {Replica} is sent a duplicate ({Format(Which)})";
}

internal sealed record IsolateStep(int Replica) : SessionStep
{
    public override void Run(Network network, Glyphs glyphs) => network.Isolate(Replica);

    public override string ToString() => $"replica {Replica} goes offline";
}

internal sealed record ReconnectStep(int Replica) : SessionStep
{
    public override void Run(Network network, Glyphs glyphs)
    {
        if (network.IsIsolated(Replica))
        {
            network.Reconnect(Replica);
        }
    }

    public override string ToString() => $"replica {Replica} reconnects";
}

/// <summary>
/// A replica replaced by a reload of its own encoded state, as the server rebuilds a description from its snapshot.
/// </summary>
internal sealed record ReloadStep(int Replica) : SessionStep
{
    public override void Run(Network network, Glyphs glyphs) => network.Reload(Replica);

    public override string ToString() => $"replica {Replica} is reloaded from its own state";
}