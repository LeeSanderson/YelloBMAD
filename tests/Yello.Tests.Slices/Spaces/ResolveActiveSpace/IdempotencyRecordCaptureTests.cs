using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Xunit;
using Yello.Application.Spaces;
using Yello.Domain.Projects;
using Yello.Host.Spaces;
using Yello.Infrastructure.Persistence;

namespace Yello.Tests.Slices.Spaces.ResolveActiveSpace;

/// <summary>
/// Story 2.2 Task 8 (AC12, <c>deferred-work.md:465-470</c>) and Q7 - the idempotency record refuses to be
/// saved beside anything else, so a duplicate key it meets can only be its own; and the not-found floor's
/// setting reads as its remarks say.
/// </summary>
/// <remarks>
/// No database: the record's check runs before any statement, and the context is never opened. A body the
/// record cannot replay is refused by <c>IdempotentResponses.Capture</c> here; the other two latent defects
/// (a non-<c>IResult</c> handler, <c>Results.Created</c>) are held by the architecture suite's ARCH-009 and
/// ARCH-010.
/// </remarks>
[Trait("Suite", "Slices")]
[Trait("Priority", "P0")]
[Trait("Requirement", "AD-18")]
[Trait("Requirement", "AR-25")]
public sealed class IdempotencyRecordCaptureTests
{
    /// <summary>
    /// AC12 - a store that left its effect unsaved has it refused, not flushed beside the record, where a
    /// duplicate key in it would be reported as <c>409 idempotency-key-in-flight</c>.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Recording_beside_an_unsaved_effect_is_refused_rather_than_reported_as_in_flight()
    {
        await using var context = new YelloDbContext(new DbContextOptionsBuilder<YelloDbContext>().UseSqlServer().Options);

        context.Projects.Add(new Project { Id = Guid.NewGuid(), SpaceId = Guid.NewGuid(), Name = "Left unsaved", CreatedAt = DateTimeOffset.UnixEpoch });

        var store = new IdempotencyStore(context, new SequentialGuidIdentifierGenerator(), TimeProvider.System);

        var refused = await Assert.ThrowsAsync<InvalidOperationException>(() => store.TryRecordAsync(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "a-key",
            new RecordedResponse("fingerprint", 201, "application/json", null, "{}"),
            TestContext.Current.CancellationToken));

        Assert.Contains(nameof(Project), refused.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// AC12 - a success whose body is not a value (text, bytes, a file, a stream) is refused rather than
    /// recorded with an empty body that every replay would answer.
    /// </summary>
    [Fact]
    public void A_success_whose_body_the_record_cannot_replay_is_refused()
    {
        var refused = Assert.Throws<InvalidOperationException>(() => IdempotentResponses.Capture(
            new DefaultHttpContext(),
            "fingerprint",
            Results.Text("created", statusCode: StatusCodes.Status201Created)));

        Assert.Contains("cannot replay", refused.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// The anchor for <see cref="A_success_whose_body_the_record_cannot_replay_is_refused"/>: a success with
    /// no body at all is recorded, as it is.
    /// </summary>
    [Fact]
    public void A_success_with_no_body_is_recorded_without_one()
    {
        var record = IdempotentResponses.Capture(new DefaultHttpContext(), "fingerprint", Results.NoContent());

        Assert.Equal(StatusCodes.Status204NoContent, record.StatusCode);
        Assert.Equal(string.Empty, record.Body);
    }

    /// <summary>
    /// Q7 (a) - the floor defaults when unset, takes a whole number of milliseconds (0 turns it off), and
    /// falls back to the default, saying why, on anything else - a value above the longest floor included.
    /// </summary>
    /// <param name="configured">The configured value.</param>
    /// <param name="milliseconds">The floor read.</param>
    /// <param name="problem">Whether the value was refused.</param>
    [Theory]
    [InlineData(null, 50, false)]
    [InlineData("", 50, false)]
    [InlineData("80", 80, false)]
    [InlineData("0", 0, false)]
    [InlineData("-5", 50, true)]
    [InlineData("fast", 50, true)]
    [InlineData("2000", 2000, false)]
    [InlineData("2001", 50, true)]
    [InlineData("2000000000", 50, true)]
    [Trait("Requirement", "AD-3")]
    public void The_not_found_floor_reads_whole_milliseconds_and_defaults_otherwise(string? configured, int milliseconds, bool problem)
    {
        var setting = NotAvailableFloor.ReadSetting(configured);

        Assert.Equal(TimeSpan.FromMilliseconds(milliseconds), setting.Floor);
        Assert.Equal(problem, setting.Problem is not null);
    }
}
