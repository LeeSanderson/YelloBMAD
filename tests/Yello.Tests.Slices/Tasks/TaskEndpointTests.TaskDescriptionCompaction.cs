using Xunit;
using Yello.Merge;
using Yello.Tests.Shared;
using static Yello.Tests.Slices.Tasks.TaskDescriptionSliceWire;
using static Yello.Tests.Slices.Tasks.TaskWire;

namespace Yello.Tests.Slices.Tasks;

/// <summary>
/// Compaction preserves SM-5: the write that brings the changes since the last snapshot to the threshold also appends one
/// snapshot row in its transaction; no existing row is altered and no change row is removed, so every author, name and time
/// survives; the superseded snapshot is the only row ever deleted; and the projection equals the text rebuilt from the latest
/// snapshot plus the changes after it.
/// </summary>
/// <remarks>
/// The threshold is lowered to <see cref="LoweredThreshold"/> through the Host setting the story names
/// (<see cref="TaskDescriptionWire.CompactionThresholdSetting"/>), the class's second pooled settings set.
/// </remarks>
public sealed partial class TaskEndpointTests
{
    private const int LoweredThreshold = 5;

    private const string NoCompaction = "the description change route and its compaction do not exist";

    private static readonly Dictionary<string, string> CompactionConfigured = new(Configured, StringComparer.Ordinal)
    {
        [TaskDescriptionWire.CompactionThresholdSetting] = LoweredThreshold.ToString(System.Globalization.CultureInfo.InvariantCulture),
    };

    /// <summary>
    /// M-11: two authors write across the threshold, then across it again. After each crossing, every change row written
    /// before it is byte-identical (payload, author, name, time), the per-author counts are unchanged, exactly one snapshot
    /// row exists with no author, and the projection equals the text rebuilt from the snapshot plus the later rows.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC9, M-11, Q7 - " + NoCompaction)]
    [Trait("Requirement", "AR-19")]
    [Trait("Requirement", "AD-13")]
    [Trait("Requirement", "SM-5")]
    [Trait("Priority", "P1")]
    public async Task Compaction_keeps_every_authored_change_row_and_its_time_and_the_projection_rebuilds_from_the_snapshot()
    {
        var database = Available();
        Actor? owner = null;
        Actor? member = null;
        var taskId = Guid.Empty;
        IReadOnlyList<DescriptionRow> beforeFirst = [];
        IReadOnlyList<DescriptionRow> afterFirst = [];
        IReadOnlyList<DescriptionRow> afterSecond = [];

        await fixture.Hosts.RunAsync(fixture.ConnectionString!, CompactionConfigured, async client =>
        {
            owner = await RegisterAsync(client, database);
            member = await RegisterAsync(client, database);

            await AddMemberAsync(database, owner.SpaceId, member.AccountId, "Member");

            var ownerEditor = NewEditor();
            var memberEditor = NewEditor();

            taskId = await TaskAsync(owner);

            // Below the threshold: two by the Owner, two by the Member, one change per batch.
            await TypeAsync(owner, owner.SpaceId, taskId, ownerEditor, "o1", "o2");
            await TypeAsync(member, owner.SpaceId, taskId, memberEditor, "m1", "m2");
            beforeFirst = await RowsAsync(database, owner.SpaceId, taskId);

            // The fifth change crosses the threshold; then five more cross it again.
            await TypeAsync(owner, owner.SpaceId, taskId, ownerEditor, "o3");
            afterFirst = await RowsAsync(database, owner.SpaceId, taskId);
            await TypeAsync(member, owner.SpaceId, taskId, memberEditor, "m3", "m4", "m5");
            await TypeAsync(owner, owner.SpaceId, taskId, ownerEditor, "o4", "o5");
            afterSecond = await RowsAsync(database, owner.SpaceId, taskId);
        });

        Assert.NotNull(owner);
        Assert.NotNull(member);

        Assert.Equal(4, beforeFirst.Count);
        Assert.All(beforeFirst, row => Assert.Equal(ChangeKind, row.Kind));

        AssertUnchanged(beforeFirst, afterFirst);
        Assert.Single(afterFirst, row => row.Kind == SnapshotKind);
        AssertUnchanged(afterFirst.Where(row => row.Kind == ChangeKind).ToList(), afterSecond);

        var snapshot = Assert.Single(afterSecond, row => row.Kind == SnapshotKind);

        Assert.Null(snapshot.AuthorAccountId);
        Assert.Null(snapshot.AuthorName);
        Assert.DoesNotContain(afterFirst.Single(row => row.Kind == SnapshotKind).Id, afterSecond.Select(row => row.Id));

        var changes = afterSecond.Where(row => row.Kind == ChangeKind).ToList();

        Assert.Equal(10, changes.Count);
        Assert.Equal(5, changes.Count(row => row.AuthorAccountId == owner.AccountId));
        Assert.Equal(5, changes.Count(row => row.AuthorAccountId == member.AccountId));
        Assert.Equal(afterSecond.Count, afterSecond.Select(row => row.Sequence).Distinct().Count());

        var projection = await DescriptionAsync(database, owner.SpaceId, taskId);

        Assert.Equal(RebuiltText(afterSecond), projection);
        Assert.Equal(RebuiltText([.. changes]), projection);
    }

    /// <summary>
    /// The snapshot covers every row before it: loaded alone, it holds the text of the changes up to its sequence, so the
    /// rebuild never needs a row before it.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC9 - " + NoCompaction)]
    [Trait("Requirement", "AR-19")]
    [Trait("Requirement", "AD-13")]
    [Trait("Priority", "P1")]
    public async Task The_snapshot_holds_the_text_of_every_change_before_it()
    {
        var database = Available();
        Actor? owner = null;
        var taskId = Guid.Empty;

        await fixture.Hosts.RunAsync(fixture.ConnectionString!, CompactionConfigured, async client =>
        {
            owner = await RegisterAsync(client, database);
            var editor = NewEditor();

            taskId = await TaskAsync(owner);
            await TypeAsync(owner, owner.SpaceId, taskId, editor, "a", "b", "c", "d", "e");
        });

        Assert.NotNull(owner);

        var rows = await RowsAsync(database, owner.SpaceId, taskId);
        var snapshot = Assert.Single(rows, row => row.Kind == SnapshotKind);
        var loaded = Strategy.LoadReplica(new ReplicaId(Guid.NewGuid()), snapshot.Payload);

        Assert.Equal(ChangeAdmission.Admitted, loaded.Admission);
        Assert.True(rows.Where(row => row.Kind == ChangeKind).All(row => row.Sequence < snapshot.Sequence), "A change row follows the snapshot written in its own transaction.");
        Assert.Equal("abcde", loaded.Value!.Text);
        Assert.Equal("abcde", await DescriptionAsync(database, owner.SpaceId, taskId));
    }

    /// <summary>
    /// Types each text at the end of the editor's text, one batch per text, each answered with nothing refused.
    /// </summary>
    private static async Task TypeAsync(Actor actor, Guid spaceId, Guid taskId, ITextReplica editor, params string[] texts)
    {
        foreach (var text in texts)
        {
            var change = editor.Insert(editor.Text.Length, text);

            AssertAnswered(await SendChangesAsync(actor, spaceId, taskId, BatchOf(editor, change)), refused: 0);
        }
    }

    /// <summary>
    /// Every row of <paramref name="before"/> is in <paramref name="after"/> with the same bytes, author, name and time.
    /// </summary>
    private static void AssertUnchanged(IReadOnlyList<DescriptionRow> before, IReadOnlyList<DescriptionRow> after)
    {
        foreach (var row in before)
        {
            var kept = Assert.Single(after, candidate => candidate.Id == row.Id);

            Assert.Equal((row.Sequence, row.Kind, row.AuthorAccountId, row.AuthorName, row.RecordedAt), (kept.Sequence, kept.Kind, kept.AuthorAccountId, kept.AuthorName, kept.RecordedAt));
            Assert.Equal(row.Payload, kept.Payload);
        }
    }
}
