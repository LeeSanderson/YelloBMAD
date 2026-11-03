using Yello.Infrastructure.Memberships;
using Yello.Infrastructure.Outbox;

namespace Yello.Infrastructure.Persistence;

/// <summary>
/// What a unit of work tells once its transaction has committed - the outbox kick (story 4.1, AC6) and the
/// <c>MembershipChanged</c> publish (story 4.4, AC6) - and tells when it ends without committing.
/// </summary>
/// <param name="OutboxSignal">The request's outbox signal, when the unit of work can enqueue email.</param>
/// <param name="MembershipChanges">The request's <c>MembershipChanged</c> publisher.</param>
internal sealed record CommitHooks(OutboxCommitSignal? OutboxSignal, MembershipChangePublisher? MembershipChanges)
{
    /// <summary>
    /// The transaction has committed.
    /// </summary>
    internal void Committed()
    {
        OutboxSignal?.Committed();
        MembershipChanges?.Committed();
    }

    /// <summary>
    /// The unit of work ended without committing.
    /// </summary>
    internal void Discarded() => MembershipChanges?.Discarded();
}
