using System.Globalization;
using Xunit;
using Yello.Client.Refusals;
using Yello.Contracts;

namespace Yello.Tests.Architecture;

/// <summary>
/// Story 2.4's half of the client refusal contract: each refusal a Label surface or the Label picker can meet renders
/// copy of its own - never the generic "Not available.", which would tell a person their Label or Task is gone when a
/// name, a colour or an id was refused, or the Label is in use - and a Member's refused definition states the
/// <c>manage-labels</c> capability's copy. AC9, AC11, AC14; UX-DR32, UX-DR37. Written by the story 2.4 ATDD run.
/// </summary>
/// <remarks>
/// The exact sentences are the dev phase's (story Task 10, "Copy"); these cases hold only that each type has its own key
/// and copy. The types stay literals: they are the wire values the client meets.
/// </remarks>
public sealed partial class RefusalCopyTests
{
    /// <summary>
    /// AC11, AC14 - each of the five Label problem types renders copy of its own, not the not-found copy (unknown types
    /// fall through to "Not available.", which would be untrue here).
    /// </summary>
    /// <param name="problemType">The problem type.</param>
    [Theory]
    [InlineData("urn:yello:problem:invalid-label-name")]
    [InlineData("urn:yello:problem:invalid-label-colour")]
    [InlineData("urn:yello:problem:label-name-taken")]
    [InlineData("urn:yello:problem:label-in-use")]
    [InlineData("urn:yello:problem:invalid-task-labels")]
    [Trait("Requirement", "UX-DR37")]
    public void A_Label_refusal_renders_its_own_copy(string problemType)
    {
        var message = RefusalCopy.For(new ProblemResponse(problemType, Errors: null));

        Assert.NotEqual(RefusalCopy.NotAvailableKey, message.Key);
        Assert.False(string.IsNullOrWhiteSpace(Copy.GetString(message.Key, CultureInfo.InvariantCulture)), $"No copy for {message.Key}.");
    }

    /// <summary>
    /// AC9, AC14 - a Member's refused definition or deletion (<c>403 insufficient-role</c>, capability
    /// <c>manage-labels</c>) renders "Only Owners and Admins can manage Labels.", the copy the Labels surface shows.
    /// <b>Active: the copy and its capability mapping already exist</b>, so this is a standing gate for the
    /// sentence story 2.4's surfaces render.
    /// </summary>
    [Fact]
    [Trait("Requirement", "AD-1")]
    public void A_Members_refused_Label_definition_renders_the_manage_labels_copy()
    {
        var message = RefusalCopy.For(new ProblemResponse(ProblemTypes.InsufficientRole, Errors: null, Capability: "manage-labels"));

        Assert.Equal("Only Owners and Admins can manage Labels.", Copy.GetString(message.Key, CultureInfo.InvariantCulture));
    }
}
