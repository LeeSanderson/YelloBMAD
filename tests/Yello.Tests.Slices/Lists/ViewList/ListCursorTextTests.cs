using System.Buffers.Text;
using System.Text;
using Xunit;
using Yello.Application.Lists;
using Yello.Domain.Lists;

namespace Yello.Tests.Slices.Lists.ViewList;

/// <summary>
/// Story 2.10 Q1 (a), AC12 - the List cursor reads back exactly the pair it was written from, under the sort it was
/// written for, in both segments; and refuses, by its text alone, anything the route did not issue for that sort.
/// </summary>
[Trait("Suite", "Slices")]
[Trait("Priority", "P1")]
[Trait("Requirement", "AR-22")]
[Trait("Requirement", "AD-29")]
public sealed class ListCursorTextTests
{
    private static readonly Guid TaskId = Guid.Parse("4f9c4a0e-2a5b-4d2e-9c3a-1d2e3f405162");

    /// <summary>
    /// Each sort's pair, valued and in the trailing segment, reads back as written.
    /// </summary>
    [Fact]
    public void A_cursor_reads_back_the_pair_it_was_written_from()
    {
        ListCursor[] dueDate = [new(TaskId, DueDate: new DateTimeOffset(2026, 11, 5, 0, 0, 0, TimeSpan.Zero)), new(TaskId)];
        ListCursor[] label = [new(TaskId, FirstLabelName: "Alpha | beta\nwith a newline"), new(TaskId)];
        var status = new ListCursor(TaskId, StatusId: Guid.NewGuid());

        Assert.All(dueDate, cursor => Assert.Equal(cursor, RoundTrip(ListSort.DueDate, cursor)));
        Assert.All(label, cursor => Assert.Equal(cursor, RoundTrip(ListSort.Label, cursor)));
        Assert.Equal(status, RoundTrip(ListSort.Status, status));
    }

    /// <summary>
    /// A cursor written for one sort is refused under the others; garbage, an empty or over-long cursor, and a Status
    /// cursor in a trailing segment are refused. Anchor: the same cursor reads under its own sort.
    /// </summary>
    [Fact]
    public void A_cursor_not_issued_for_the_sort_is_refused()
    {
        var issued = ListCursorText.Write(ListSort.DueDate, new ListCursor(TaskId, DueDate: DateTimeOffset.UnixEpoch.AddYears(40)));
        var trailingStatus = Base64Url.EncodeToString(Encoding.UTF8.GetBytes($"1\nstatus\n{TaskId:N}\nn\n"));
        var anotherVersion = Base64Url.EncodeToString(Encoding.UTF8.GetBytes($"2\ndue-date\n{TaskId:N}\nn\n"));

        Assert.True(ListCursorText.TryRead(ListSort.DueDate, issued, out _));
        Assert.False(ListCursorText.TryRead(ListSort.Label, issued, out _));
        Assert.False(ListCursorText.TryRead(ListSort.Status, issued, out _));
        Assert.False(ListCursorText.TryRead(ListSort.DueDate, "not a cursor at all", out _));
        Assert.False(ListCursorText.TryRead(ListSort.DueDate, string.Empty, out _));
        Assert.False(ListCursorText.TryRead(ListSort.DueDate, new string('A', 4_000), out _));
        Assert.False(ListCursorText.TryRead(ListSort.Status, trailingStatus, out _));
        Assert.False(ListCursorText.TryRead(ListSort.DueDate, anotherVersion, out _));
    }

    private static ListCursor? RoundTrip(ListSort sort, ListCursor cursor)
    {
        Assert.True(ListCursorText.TryRead(sort, ListCursorText.Write(sort, cursor), out var read));

        return read;
    }
}
