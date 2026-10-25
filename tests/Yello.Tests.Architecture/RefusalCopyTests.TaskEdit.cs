using System.Globalization;
using Xunit;
using Yello.Client.Refusals;
using Yello.Contracts;

namespace Yello.Tests.Architecture;

/// <summary>
/// Story 2.3's half of the client refusal contract: the two validation refusals an edit can meet render copy
/// of their own - never the generic "Not available.", which would tell a person their Task is gone when only
/// a field was refused - and a Viewer's refused edit states the <c>manage-tasks</c> capability's copy (AC12,
/// UX-DR32, UX-DR37). Written by the story 2.3 ATDD run.
/// </summary>
/// <remarks>
/// The exact sentences are the dev phase's (Task 9, "Copy");
/// these cases hold only that each has its own key and copy.
/// </remarks>
public sealed partial class RefusalCopyTests
{
    /// <summary>
    /// AC12 - a <c>400 invalid-task-status</c> renders copy of its own, not the not-found copy.
    /// </summary>
    [Fact]
    [Trait("Requirement", "AR-34")]
    [Trait("Requirement", "UX-DR32")]
    public void An_invalid_Status_refusal_renders_its_own_copy()
    {
        var message = RefusalCopy.For(new ProblemResponse(ProblemTypes.InvalidTaskStatus, Errors: null));

        Assert.NotEqual(RefusalCopy.NotAvailableKey, message.Key);
        Assert.False(string.IsNullOrWhiteSpace(Copy.GetString(message.Key, CultureInfo.InvariantCulture)), $"No copy for {message.Key}.");
    }

    /// <summary>
    /// AC12 - a <c>400 invalid-due-date</c> renders copy of its own, not the not-found copy.
    /// </summary>
    [Fact]
    [Trait("Requirement", "AR-34")]
    [Trait("Requirement", "UX-DR32")]
    public void An_invalid_due_date_refusal_renders_its_own_copy()
    {
        var message = RefusalCopy.For(new ProblemResponse(ProblemTypes.InvalidDueDate, Errors: null));

        Assert.NotEqual(RefusalCopy.NotAvailableKey, message.Key);
        Assert.False(string.IsNullOrWhiteSpace(Copy.GetString(message.Key, CultureInfo.InvariantCulture)), $"No copy for {message.Key}.");
    }

    /// <summary>
    /// AC12, AC3 - a Viewer's refused edit (<c>403 insufficient-role</c>, capability <c>manage-tasks</c>) renders
    /// "Viewers cannot edit Tasks.", the copy the panel shows inline. <b>Active: the copy and its mapping exist
    /// since story 2.2</b>, so this is a standing gate for the sentence the panel's revert renders.
    /// </summary>
    [Fact]
    [Trait("Requirement", "UX-DR32")]
    [Trait("Requirement", "AD-1")]
    public void A_Viewers_refused_edit_renders_the_manage_tasks_copy()
    {
        var message = RefusalCopy.For(new ProblemResponse(ProblemTypes.InsufficientRole, Errors: null, Capability: "manage-tasks"));

        Assert.Equal("Viewers cannot edit Tasks.", Copy.GetString(message.Key, CultureInfo.InvariantCulture));
    }
}
