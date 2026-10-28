using Xunit;
using Yello.Client.Components;

namespace Yello.Tests.Architecture;

/// <summary>
/// Story 2.10's List View row traversal as pure logic (Task 6, open question 8 (a)): <c>↑</c>/<c>↓</c> between loaded
/// rows, <c>Home</c>/<c>End</c> to the first and last loaded rows, and <c>↓</c> on the last loaded row asking for the next
/// page - only when one follows and none is out. AC7, AC18; UX-DR24.
/// </summary>
/// <remarks>
/// <b>In this suite because it is the only one that sees <c>Yello.Client</c>'s types</b> (<c>TESTING-CONVENTIONS.md</c>,
/// "Where a <c>Yello.Client</c> unit test goes"), as <c>BoardTraversalTests</c> is. The browser cases
/// (<c>ListViewTests</c>) press the same keys on the rendered list.
/// </remarks>
[Trait("Suite", "Architecture")]
[Trait("Priority", "P1")]
[Trait("Requirement", "UX-DR24")]
[Trait("Requirement", "AD-29")]
public sealed class ListTraversalTests
{
    /// <summary>
    /// ↑ and ↓ move one loaded row, Home and End to the ends; at an edge, or where focus already is, nothing moves.
    /// </summary>
    /// <param name="at">The focused row.</param>
    /// <param name="key">The key.</param>
    /// <param name="expected">The step.</param>
    /// <param name="index">The row it lands on.</param>
    [Theory]
    [InlineData(2, "ArrowUp", ListStep.Move, 1)]
    [InlineData(2, "ArrowDown", ListStep.Move, 3)]
    [InlineData(0, "ArrowUp", ListStep.None, 0)]
    [InlineData(3, "Home", ListStep.Move, 0)]
    [InlineData(0, "Home", ListStep.None, 0)]
    [InlineData(1, "End", ListStep.Move, 4)]
    [InlineData(4, "End", ListStep.None, 4)]
    [InlineData(2, "ArrowLeft", ListStep.None, 2)]
    [InlineData(2, "Tab", ListStep.None, 2)]
    public void Keys_move_between_loaded_rows(int at, string key, ListStep expected, int index)
    {
        Assert.Equal((expected, index), ListTraversal.Next(5, at, key, hasMore: true, loading: false));
    }

    /// <summary>
    /// AC7 - ↓ on the last loaded row asks for the next page when one follows and none is out; with nothing after it, or a
    /// page already out (2.9 AC11's rule), it does nothing.
    /// </summary>
    [Fact]
    public void Down_on_the_last_loaded_row_loads_the_next_page_only_when_one_follows_and_none_is_out()
    {
        Assert.Equal((ListStep.LoadMore, 4), ListTraversal.Next(5, 4, "ArrowDown", hasMore: true, loading: false));
        Assert.Equal((ListStep.None, 4), ListTraversal.Next(5, 4, "ArrowDown", hasMore: false, loading: false));
        Assert.Equal((ListStep.None, 4), ListTraversal.Next(5, 4, "ArrowDown", hasMore: true, loading: true));
    }

    /// <summary>
    /// A row the list no longer holds, or an empty list, moves nothing.
    /// </summary>
    [Fact]
    public void A_row_outside_the_loaded_rows_moves_nothing()
    {
        Assert.Equal((ListStep.None, -1), ListTraversal.Next(5, -1, "ArrowDown", hasMore: true, loading: false));
        Assert.Equal((ListStep.None, 0), ListTraversal.Next(0, 0, "Home", hasMore: true, loading: false));
    }
}
