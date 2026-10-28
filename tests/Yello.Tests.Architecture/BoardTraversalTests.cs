using Xunit;
using Yello.Client.Components;

namespace Yello.Tests.Architecture;

/// <summary>
/// Story 2.8's traversal and carry rules as pure logic: <c>↑</c>/<c>↓</c> within a column, clamped and resetting the run's
/// origin; <c>←</c>/<c>→</c> between columns by LOGICAL direction, landing at the sticky origin index clamped to each column's
/// length, or on an empty column itself; and a carried Task's slot, counted without it, entering each column at the same
/// ordinal clamped to that column's length. AC1, AC2, AC5; UX-DR24, UX-DR42.
/// </summary>
/// <remarks>
/// <b>In this suite because it is the only one that sees <c>Yello.Client</c>'s types</b>
/// (<c>TESTING-CONVENTIONS.md</c>, "Where a <c>Yello.Client</c> unit test goes"). The browser cases (<c>KeyboardTests</c>)
/// press the same keys on the rendered Board.
/// </remarks>
[Trait("Suite", "Architecture")]
[Trait("Priority", "P1")]
[Trait("Requirement", "UX-DR24")]
public sealed class BoardTraversalTests
{
    // Columns of 5, 1, 0 and 3 Tasks: the story's sticky-origin example.
    private static readonly int[] Ragged = [5, 1, 0, 3];

    /// <summary>
    /// AC2 - a run that starts at index 4 lands at 0 in a column of 1, on the empty column itself, at 2 (the third card) in
    /// a column of 3, and back at 4 in the column of 5: the origin is remembered, never re-derived per hop.
    /// </summary>
    [Fact]
    public void A_run_across_ragged_columns_keeps_its_sticky_origin()
    {
        (int Column, int? Index) stop = (0, 4);
        var origin = 4;
        var landed = new List<(int Column, int? Index)>();

        foreach (var key in new[] { BoardKey.InlineEnd, BoardKey.InlineEnd, BoardKey.InlineEnd, BoardKey.InlineStart, BoardKey.InlineStart, BoardKey.InlineStart })
        {
            (stop, origin) = BoardTraversal.Traverse(Ragged, stop, key, origin);
            landed.Add(stop);
        }

        Assert.Equal(
            new List<(int Column, int? Index)> { (1, 0), (2, null), (3, 2), (2, null), (1, 0), (0, 4) },
            landed);
        Assert.Equal(4, origin);
    }

    /// <summary>
    /// AC1 - <c>↑</c> and <c>↓</c> clamp at the ends of a column (no wrap) and reset the origin to where they land.
    /// </summary>
    [Fact]
    public void Up_and_down_clamp_at_the_ends_and_reset_the_origin()
    {
        Assert.Equal(((0, 0), 0), BoardTraversal.Traverse(Ragged, (0, 0), BoardKey.Up, 3));
        Assert.Equal(((0, 4), 4), BoardTraversal.Traverse(Ragged, (0, 4), BoardKey.Down, 1));
        Assert.Equal(((0, 2), 2), BoardTraversal.Traverse(Ragged, (0, 3), BoardKey.Up, 4));
        Assert.Equal(((3, 1), 1), BoardTraversal.Traverse(Ragged, (3, 0), BoardKey.Down, 4));
    }

    /// <summary>
    /// AC1 - <c>←</c> and <c>→</c> stop at the first and last column; an empty column's <c>↑</c>/<c>↓</c> stay on it.
    /// </summary>
    [Fact]
    public void Inline_keys_stop_at_the_first_and_last_column()
    {
        Assert.Equal(((0, 2), 2), BoardTraversal.Traverse(Ragged, (0, 2), BoardKey.InlineStart, 2));
        Assert.Equal(((3, 1), 1), BoardTraversal.Traverse(Ragged, (3, 1), BoardKey.InlineEnd, 1));
        Assert.Equal(((2, null), 3), BoardTraversal.Traverse(Ragged, (2, null), BoardKey.Down, 3));
    }

    /// <summary>
    /// AC1 - a column focused as a whole (after a card in it was deleted) goes to its first card on <c>↓</c>.
    /// </summary>
    [Fact]
    public void A_column_focused_as_a_whole_goes_to_its_first_card()
    {
        Assert.Equal(((3, 0), 0), BoardTraversal.Traverse(Ragged, (3, null), BoardKey.Down, 2));
    }

    /// <summary>
    /// AC1, UX-DR42 - the arrows map to logical directions: <c>ArrowRight</c> is the inline end left to right and the inline
    /// start right to left, and <c>ArrowLeft</c> the reverse; <c>↑</c>/<c>↓</c> never mirror; other keys are not traversal.
    /// </summary>
    [Fact]
    [Trait("Requirement", "UX-DR42")]
    public void Arrow_keys_map_to_logical_directions_and_mirror_under_right_to_left()
    {
        Assert.Equal(BoardKey.InlineEnd, BoardTraversal.KeyOf("ArrowRight", rightToLeft: false));
        Assert.Equal(BoardKey.InlineStart, BoardTraversal.KeyOf("ArrowLeft", rightToLeft: false));
        Assert.Equal(BoardKey.InlineStart, BoardTraversal.KeyOf("ArrowRight", rightToLeft: true));
        Assert.Equal(BoardKey.InlineEnd, BoardTraversal.KeyOf("ArrowLeft", rightToLeft: true));
        Assert.Equal(BoardKey.Up, BoardTraversal.KeyOf("ArrowUp", rightToLeft: true));
        Assert.Equal(BoardKey.Down, BoardTraversal.KeyOf("ArrowDown", rightToLeft: true));
        Assert.Equal(BoardKey.Space, BoardTraversal.KeyOf(" ", rightToLeft: false));
        Assert.Equal(BoardKey.Escape, BoardTraversal.KeyOf("Escape", rightToLeft: false));
        Assert.Equal(BoardKey.Tab, BoardTraversal.KeyOf("Tab", rightToLeft: false));
        Assert.Equal(BoardKey.Other, BoardTraversal.KeyOf("Enter", rightToLeft: false));
    }

    /// <summary>
    /// AC5 - a Task picked up at position 3 enters a column of 1 at its bottom slot (1), a column of 5 at slot 2 (sticky),
    /// and <c>↑</c> adjusts it to 1 before the drop, resetting the origin. Counts exclude the carried Task.
    /// </summary>
    [Fact]
    public void A_carried_Task_lands_at_the_same_ordinal_clamped_and_adjusts_with_up_and_down()
    {
        // The carried Task came from column 0 (5 others there now), slot 2.
        int[] others = [4, 1, 5];

        var (column, slot, origin) = BoardTraversal.Carry(others, 0, 2, BoardKey.InlineEnd, 2);
        Assert.Equal((1, 1, 2), (column, slot, origin));

        (column, slot, origin) = BoardTraversal.Carry(others, column, slot, BoardKey.InlineEnd, origin);
        Assert.Equal((2, 2, 2), (column, slot, origin));

        (column, slot, origin) = BoardTraversal.Carry(others, column, slot, BoardKey.Up, origin);
        Assert.Equal((2, 1, 1), (column, slot, origin));
    }

    /// <summary>
    /// AC5 - an empty column takes slot 0; the bottom slot (the column's length, the carried Task excluded) is valid; the
    /// first and last columns stop the carry.
    /// </summary>
    [Fact]
    public void A_carry_takes_slot_zero_in_an_empty_column_and_stops_at_the_ends()
    {
        int[] others = [2, 0, 3];

        Assert.Equal((1, 0, 4), BoardTraversal.Carry(others, 0, 2, BoardKey.InlineEnd, 4));
        Assert.Equal((0, 2, 2), BoardTraversal.Carry(others, 0, 1, BoardKey.Down, 1));
        Assert.Equal((0, 2, 2), BoardTraversal.Carry(others, 0, 2, BoardKey.Down, 2));
        Assert.Equal((0, 0, 0), BoardTraversal.Carry(others, 0, 0, BoardKey.Up, 0));
        Assert.Equal((0, 1, 1), BoardTraversal.Carry(others, 0, 1, BoardKey.InlineStart, 1));
        Assert.Equal((2, 3, 9), BoardTraversal.Carry(others, 2, 3, BoardKey.InlineEnd, 9));
    }

    /// <summary>
    /// Story 2.11, AC12, open question 5 (a) - below 768px the pager is the only route to another Status: <c>←</c>/<c>→</c>
    /// in the composite move nothing, traversing or carrying, from a card or from an empty column, and keep the origin.
    /// </summary>
    [Fact]
    [Trait("Requirement", "UX-DR20")]
    [Trait("Requirement", "UX-DR41")]
    public void Below_768px_the_inline_keys_move_nothing_traversing_or_carrying()
    {
        foreach (var key in new[] { BoardKey.InlineStart, BoardKey.InlineEnd })
        {
            Assert.Equal(((1, 0), 3), BoardTraversal.Traverse(Ragged, (1, 0), key, 3, narrow: true));
            Assert.Equal(((2, null), 1), BoardTraversal.Traverse(Ragged, (2, null), key, 1, narrow: true));
            Assert.Equal((1, 1, 2), BoardTraversal.Carry([4, 1, 5], 1, 1, key, 2, narrow: true));
        }

        // The same keys at 768px and above are 2.8's, unchanged.
        Assert.Equal(((2, null), 3), BoardTraversal.Traverse(Ragged, (1, 0), BoardKey.InlineEnd, 3, narrow: false));
        Assert.Equal((2, 2, 2), BoardTraversal.Carry([4, 1, 5], 1, 1, BoardKey.InlineEnd, 2, narrow: false));
    }

    /// <summary>
    /// Story 2.11, AC12 - below 768px <c>↑</c>/<c>↓</c> traverse and carry within the column exactly as at any width.
    /// </summary>
    [Fact]
    [Trait("Requirement", "UX-DR20")]
    public void Below_768px_up_and_down_are_unchanged()
    {
        foreach (var key in new[] { BoardKey.Up, BoardKey.Down })
        {
            Assert.Equal(BoardTraversal.Traverse(Ragged, (0, 2), key, 4), BoardTraversal.Traverse(Ragged, (0, 2), key, 4, narrow: true));
            Assert.Equal(BoardTraversal.Carry([4, 1, 5], 0, 2, key, 2), BoardTraversal.Carry([4, 1, 5], 0, 2, key, 2, narrow: true));
        }
    }

    /// <summary>
    /// Story 2.11, AC4, AC8, AC9, open question 3 (a) - the pager's keys: the inline keys step one tab by logical direction,
    /// <c>Home</c>/<c>End</c> go to the first and last, and the ends clamp (no wrap). Under right to left the physical arrows
    /// mirror through <see cref="BoardTraversal.KeyOf"/>; the tab order - the Status sequence - never does.
    /// </summary>
    [Fact]
    [Trait("Requirement", "UX-DR20")]
    [Trait("Requirement", "UX-DR42")]
    public void The_pager_keys_step_clamp_and_mirror_under_right_to_left()
    {
        Assert.Equal(BoardKey.Home, BoardTraversal.KeyOf("Home", rightToLeft: false));
        Assert.Equal(BoardKey.End, BoardTraversal.KeyOf("End", rightToLeft: true));

        // Left to right: → is the next Status, ← the previous.
        Assert.Equal(2, BoardTraversal.Pager(5, 1, BoardTraversal.KeyOf("ArrowRight", rightToLeft: false)));
        Assert.Equal(0, BoardTraversal.Pager(5, 1, BoardTraversal.KeyOf("ArrowLeft", rightToLeft: false)));

        // Right to left: ← is the next Status, → the previous.
        Assert.Equal(2, BoardTraversal.Pager(5, 1, BoardTraversal.KeyOf("ArrowLeft", rightToLeft: true)));
        Assert.Equal(0, BoardTraversal.Pager(5, 1, BoardTraversal.KeyOf("ArrowRight", rightToLeft: true)));

        // The ends clamp.
        Assert.Equal(0, BoardTraversal.Pager(5, 0, BoardKey.InlineStart));
        Assert.Equal(4, BoardTraversal.Pager(5, 4, BoardKey.InlineEnd));

        // Home and End, either direction.
        Assert.Equal(0, BoardTraversal.Pager(5, 3, BoardKey.Home));
        Assert.Equal(4, BoardTraversal.Pager(5, 1, BoardKey.End));

        // Any other key leaves the selection where it is.
        Assert.Equal(3, BoardTraversal.Pager(5, 3, BoardKey.Up));
        Assert.Equal(3, BoardTraversal.Pager(5, 3, BoardKey.Other));
    }
}
