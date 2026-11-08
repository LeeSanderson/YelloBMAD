using Microsoft.EntityFrameworkCore;
using Xunit;
using Yello.Application.Outbox;
using Yello.Infrastructure.Outbox;
using Yello.Infrastructure.Persistence;

namespace Yello.Tests.Slices.Invitations.IssueInvitation;

/// <summary>
/// The send record names the Space of the request that enqueued the email, so an enqueue with no Space to name - outside a
/// Space-scoped unit of work - is refused before anything is added, never recorded without a Space. A later enqueue from an
/// Account-scoped request meets this case and decides where its record belongs deliberately.
/// </summary>
/// <remarks>
/// No database: the refusal comes before the first statement. The wire cases in
/// <c>ActiveSpaceEndpointTests.AssignTask</c> hold the records written in a Space.
/// </remarks>
[Trait("Suite", "Slices")]
[Trait("Priority", "P1")]
[Trait("Requirement", "FS-NFR-2")]
public sealed class OutboxSendRecordTests
{
    /// <summary>
    /// With no Space resolved, the enqueue throws and leaves nothing tracked: no send record and no outbox row.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task An_enqueue_outside_a_Space_scoped_unit_of_work_is_refused_rather_than_recorded_without_a_Space()
    {
        await using var context = new YelloDbContext(new DbContextOptionsBuilder<YelloDbContext>().UseSqlServer().Options);

        var store = new OutboxStore(context, new SequentialGuidIdentifierGenerator(), TimeProvider.System, new OutboxCommitSignal(new NoRecovery()));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            store.EnqueueAsync(new OutboxEmail(OutboxKinds.TaskAssigned, "someone@example.test", "Subject", "Body"), TestContext.Current.CancellationToken));

        Assert.Empty(context.ChangeTracker.Entries());
    }

    private sealed class NoRecovery : IOutboxRecovery
    {
        public void SweepNow()
        {
            // Nothing is enqueued, so nothing is ever swept.
        }

        public bool SweepIfDue() => false;
    }
}
