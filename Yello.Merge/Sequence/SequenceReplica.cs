namespace Yello.Merge.Sequence;

/// <summary>
/// One replica of a plain-text sequence merge: every character has a unique identity, inserts name the character they
/// follow, deletes name the characters they remove, and deleted characters stay as hidden placeholders. Applying the same
/// set of changes in any order gives the same text.
/// </summary>
/// <remarks>
/// A change whose anchor or targets have not arrived is held, counted in <see cref="Version"/> and applied as soon as they
/// do, so delivery order never matters and nothing is ever asked for twice.
/// </remarks>
internal sealed class SequenceReplica(ReplicaId id) : ITextReplica
{
    private readonly Document _document = new();
    private readonly HashSet<ChangeId> _held = [];
    private readonly HashSet<long> _withdrawn = [];
    private readonly List<HeldChange> _applied = [];
    private readonly Dictionary<ChangeId, HeldChange> _waiting = [];
    private readonly Dictionary<ElementId, List<HeldChange>> _waitingOn = [];
    private ReplicaVersion? _version;

    public ReplicaId Id { get; } = id;

    public string Text => _document.Text;

    public ReplicaVersion Version => _version ??= HeldChanges.Of(_held).ToVersion();

    /// <summary>
    /// The last sequence number this replica issued, withdrawn ones included, so a resumed replica never issues one again.
    /// </summary>
    internal long LastSequence { get; private set; }

    /// <summary>
    /// The Lamport clock: at least every character identity this replica has applied.
    /// </summary>
    internal long Clock { get; private set; }

    internal IReadOnlyCollection<long> Withdrawn => _withdrawn;

    /// <summary>
    /// Every change held, applied ones in the order applied, then those still waiting.
    /// </summary>
    internal IEnumerable<TextChange> Held => _applied.Concat(_waiting.Values).Select(held => held.Change);

    public TextChange Insert(int index, string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        var scalars = Scalars.FromUtf16(text) ?? throw new ArgumentException("The text holds an unpaired surrogate.", nameof(text));

        if (scalars.Count == 0)
        {
            throw new ArgumentException("There is nothing to insert.", nameof(text));
        }

        RequireBoundary(index, nameof(index));

        return Commit(new InsertBody(NextChangeId(), _document.VisibleBefore(index), Clock + 1, scalars));
    }

    public TextChange Delete(int index, int length)
    {
        RequireBoundary(index, nameof(index));

        if (length <= 0 || length > Text.Length - index)
        {
            throw new ArgumentOutOfRangeException(nameof(length), length, "The range must be non-empty and inside the text.");
        }

        RequireBoundary(index + length, nameof(length));

        return Commit(new DeleteBody(NextChangeId(), _document.VisibleRuns(index, length)));
    }

    public ChangeAdmission Apply(TextChange change)
    {
        ArgumentNullException.ThrowIfNull(change);

        var (admission, body) = ChangeCodec.Decode(change.Encoded.Span);

        if (admission != ChangeAdmission.Admitted)
        {
            return admission;
        }

        if (body!.Id != change.Id || CollidesWithHeldCharacters(body))
        {
            return ChangeAdmission.Malformed;
        }

        if (_held.Contains(body.Id) || (body.Id.Origin == Id && _withdrawn.Contains(body.Id.Sequence)))
        {
            return ChangeAdmission.Admitted;
        }

        if (body.Id.Origin == Id)
        {
            LastSequence = Math.Max(LastSequence, body.Id.Sequence);
        }

        Hold(new HeldChange(change, body));

        return ChangeAdmission.Admitted;
    }

    public IReadOnlyList<TextChange> ChangesMissingFrom(ReplicaVersion version)
    {
        ArgumentNullException.ThrowIfNull(version);

        var (admission, held) = HeldChanges.Read(version.Encoded.Span);

        if (admission != ChangeAdmission.Admitted)
        {
            throw new ArgumentException($"The version is not one this build can read ({admission}).", nameof(version));
        }

        return [.. Held.Where(change => !held!.Contains(change.Id))];
    }

    public ReadOnlyMemory<byte> EncodeState() => ReplicaState.Encode(this);

    public void Withdraw(ChangeId change)
    {
        if (change.Origin != Id)
        {
            throw new ArgumentException("Only this replica's own edits can be withdrawn.", nameof(change));
        }

        var index = _applied.FindIndex(held => held.Change.Id == change);

        if (index < 0)
        {
            return;
        }

        var withdrawn = _applied[index];

        if (HasDependents(withdrawn))
        {
            throw new InvalidOperationException("A later edit builds on this one's text. Withdraw the later edits first, the latest first.");
        }

        withdrawn.Body.TakeBackFrom(_document);
        _applied.RemoveAt(index);
        _held.Remove(change);
        _withdrawn.Add(change.Sequence);
        _version = null;
    }

    /// <summary>
    /// Restores what a resumed replica must not forget: the sequence numbers and clock it already used, and which of its
    /// changes it withdrew.
    /// </summary>
    internal void Resume(long lastSequence, long clock, IEnumerable<long> withdrawn)
    {
        LastSequence = Math.Max(LastSequence, lastSequence);
        Clock = Math.Max(Clock, clock);
        _withdrawn.UnionWith(withdrawn);
    }

    private void RequireBoundary(int index, string parameter)
    {
        var text = Text;

        if (index < 0 || index > text.Length)
        {
            throw new ArgumentOutOfRangeException(parameter, index, "The index is outside the text.");
        }

        if (index > 0 && index < text.Length && char.IsLowSurrogate(text[index]))
        {
            throw new ArgumentOutOfRangeException(parameter, index, "The index falls inside a surrogate pair.");
        }
    }

    private ChangeId NextChangeId() => new(Id, ++LastSequence);

    private TextChange Commit(ChangeBody body)
    {
        var held = new HeldChange(ChangeCodec.Encode(body), body);

        Hold(held);

        return held.Change;
    }

    private void Hold(HeldChange change)
    {
        _held.Add(change.Body.Id);
        _version = null;

        var ready = new Queue<HeldChange>();

        ready.Enqueue(change);

        while (ready.TryDequeue(out var next))
        {
            if (next.Body.FirstMissing(_document.Elements) is { } missing)
            {
                Wait(next, missing);

                continue;
            }

            _waiting.Remove(next.Body.Id);
            Integrate(next, ready);
        }
    }

    private void Wait(HeldChange change, ElementId missing)
    {
        _waiting[change.Body.Id] = change;

        if (!_waitingOn.TryGetValue(missing, out var waiters))
        {
            _waitingOn[missing] = waiters = [];
        }

        waiters.Add(change);
    }

    private void Integrate(HeldChange change, Queue<HeldChange> ready)
    {
        _applied.Add(change);
        change.Body.ApplyTo(_document);

        foreach (var created in change.Body.Created())
        {
            Clock = Math.Max(Clock, created.Clock);

            if (_waitingOn.Remove(created, out var waiters))
            {
                waiters.ForEach(ready.Enqueue);
            }
        }
    }

    private bool CollidesWithHeldCharacters(ChangeBody body) =>
        !_held.Contains(body.Id) && body.Created().Any(_document.Elements.ContainsKey);

    private bool HasDependents(HeldChange withdrawn)
    {
        var created = withdrawn.Body.Created().ToList();

        return created.Count > 0
            && _applied.Concat(_waiting.Values).Any(other => !ReferenceEquals(other, withdrawn) && created.Exists(other.Body.DependsOn));
    }

    private sealed record HeldChange(TextChange Change, ChangeBody Body);
}
