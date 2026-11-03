using Microsoft.Extensions.Logging;
using Yello.Application.Memberships;

namespace Yello.Infrastructure.Memberships;

/// <summary>
/// Collects the request's <see cref="MembershipChanged"/> events and publishes them in-process once its unit of work has
/// committed. AR-16, AD-9, AD-10; story 4.4, AC6, Q7 (a), Q18 (a).
/// </summary>
/// <remarks>
/// <para>
/// <b>A post-commit hook on the unit of work</b>, the <c>OutboxCommitSignal</c> shape: the Space-scoped unit of work and the
/// Invitation unit of work call <see cref="Committed"/> after their transaction has committed and at no other time, so a
/// change that rolled back - a failure result, a thrown exception, a failed idempotency record - publishes nothing. A
/// refusal or a no-op records nothing to begin with. Scoped with the request; nothing outlives it.
/// </para>
/// <para>
/// <b>One publish per affected Account</b> (AD-9): a pair recorded twice in one unit of work publishes once. Each publish
/// is delivered to every registered <see cref="IMembershipChangeSubscriber"/> - none in production until stories 7.3 and
/// 7.7 - and writes one log line naming the Account and the Space. A subscriber that throws is logged and the rest still
/// run: the change is already committed, and the request's answer must say so.
/// </para>
/// </remarks>
/// <param name="subscribers">Every registered subscriber.</param>
/// <param name="logger">The publish log.</param>
internal sealed class MembershipChangePublisher(
    IEnumerable<IMembershipChangeSubscriber> subscribers,
    ILogger<MembershipChangePublisher> logger)
    : IMembershipChanges
{
    private readonly List<MembershipChanged> _pending = [];

    /// <inheritdoc />
    public void Record(Guid accountId, Guid spaceId)
    {
        var change = new MembershipChanged(accountId, spaceId);

        if (!_pending.Contains(change))
        {
            _pending.Add(change);
        }
    }

    /// <summary>
    /// The unit of work has committed: publish what it recorded, once.
    /// </summary>
    internal void Committed()
    {
        var published = _pending.ToList();

        _pending.Clear();

        foreach (var change in published)
        {
            MembershipLog.Published(logger, change.AccountId, change.SpaceId);

            foreach (var subscriber in subscribers)
            {
                Deliver(subscriber, change);
            }
        }
    }

    /// <summary>
    /// The unit of work ended without committing: what it recorded never happened.
    /// </summary>
    internal void Discarded() => _pending.Clear();

    private void Deliver(IMembershipChangeSubscriber subscriber, MembershipChanged change)
    {
        try
        {
            subscriber.OnMembershipChanged(change);
        }
        // The change is committed: a subscriber's unforeseen failure must neither reach the request's answer nor stop the
        // other subscribers, and an allow-list of exception types would itself be the defect (the OutboxDispatcher reason).
#pragma warning disable S2221 // "Catch a list of specific exception subtype or use exception filters"
        catch (Exception exception) when (exception is not OutOfMemoryException)
#pragma warning restore S2221
        {
            MembershipLog.SubscriberFailed(logger, change.AccountId, change.SpaceId, exception);
        }
    }
}
