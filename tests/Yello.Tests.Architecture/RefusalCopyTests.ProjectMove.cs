using System.Globalization;
using Xunit;
using Yello.Client.Refusals;
using Yello.Contracts;

namespace Yello.Tests.Architecture;

/// <summary>
/// The client's copy for moving a Task into another Project: a destination that is not another Project of the Space renders
/// copy of its own, never the generic "Not available.", which would tell a person their Task is gone.
/// </summary>
public sealed partial class RefusalCopyTests
{
    /// <summary>
    /// A <c>400 invalid-task-project</c> renders copy of its own, not the not-found copy.
    /// </summary>
    [Fact]
    [Trait("Requirement", "AR-34")]
    [Trait("Requirement", "UX-DR32")]
    public void A_bad_destination_Project_refusal_renders_its_own_copy()
    {
        var message = RefusalCopy.For(new ProblemResponse("urn:yello:problem:invalid-task-project", Errors: null));

        Assert.NotEqual(RefusalCopy.NotAvailableKey, message.Key);
        Assert.False(string.IsNullOrWhiteSpace(Copy.GetString(message.Key, CultureInfo.InvariantCulture)), $"No copy for {message.Key}.");
    }
}
