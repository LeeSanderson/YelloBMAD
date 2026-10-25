using System.Globalization;
using Xunit;
using Yello.Client.Refusals;
using Yello.Contracts;
using Yello.Domain;
using Yello.Domain.Bounds;

namespace Yello.Tests.Architecture;

/// <summary>
/// Story 2.2's half of the client refusal contract: the two idempotency refusals a creation can meet render
/// copy of their own, never the generic "Not available." (AC13, <c>deferred-work.md:460</c>), and the
/// Tasks-per-Project bound states its number (AC7). Written by the story 2.2 ATDD run.
/// </summary>
public sealed partial class RefusalCopyTests
{
    /// <summary>
    /// AC13, UX-DR32, UX-DR37 - a <c>409 idempotency-key-in-flight</c> renders "That was already sent. Reload to
    /// see it.", not the not-found copy, so a person told their request is still in flight is not told it does
    /// not exist.
    /// </summary>
    [Fact(Skip = "red: AC13 - RefusalCopy maps idempotency-key-in-flight to Not available. (Task 11)")]
    [Trait("Requirement", "AR-25")]
    public void An_in_flight_key_refusal_renders_its_own_copy()
    {
        var message = RefusalCopy.For(new ProblemResponse(ProblemTypes.IdempotencyKeyInFlight, Errors: null));

        Assert.NotEqual(RefusalCopy.NotAvailableKey, message.Key);
        Assert.Equal("That was already sent. Reload to see it.", Copy.GetString(message.Key, CultureInfo.InvariantCulture));
    }

    /// <summary>
    /// AC13, UX-DR32, UX-DR37 - a <c>422 idempotency-key-reused</c> renders "That request was already used for
    /// something else. Try again.", not the not-found copy.
    /// </summary>
    [Fact(Skip = "red: AC13 - RefusalCopy maps idempotency-key-reused to Not available. (Task 11)")]
    [Trait("Requirement", "AR-25")]
    public void A_reused_key_refusal_renders_its_own_copy()
    {
        var message = RefusalCopy.For(new ProblemResponse(ProblemTypes.IdempotencyKeyReused, Errors: null));

        Assert.NotEqual(RefusalCopy.NotAvailableKey, message.Key);
        Assert.Equal("That request was already used for something else. Try again.", Copy.GetString(message.Key, CultureInfo.InvariantCulture));
    }

    /// <summary>
    /// AC7, NFR-8 - the 5,001st Task's refusal renders "This Project has 5000 Tasks, the maximum.", the number
    /// coming from the problem's <c>limit</c>, not from the copy. <b>Active: the copy and its mapping were
    /// written ahead of 2.2 (story 1.6's bound copy), so this is a standing gate for the string Task 10
    /// renders inline.</b>
    /// </summary>
    [Fact]
    [Trait("Requirement", "AR-30")]
    [Trait("Requirement", "NFR-8")]
    public void The_Tasks_per_Project_bound_states_its_number()
    {
        var limit = BoundRegistry.For(Bound.TasksPerProject).Limit;
        var message = RefusalCopy.For(new ProblemResponse(ProblemTypes.BoundReached, null, Bound: StableCode.Of(Bound.TasksPerProject), Limit: limit));

        Assert.Equal(limit, message.Limit);
        Assert.Equal(
            "This Project has 5000 Tasks, the maximum.",
            string.Format(CultureInfo.InvariantCulture, Copy.GetString(message.Key, CultureInfo.InvariantCulture)!, message.Limit));
    }
}
