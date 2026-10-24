using Microsoft.EntityFrameworkCore;
using Yello.Application.Spaces;
using Yello.Domain.Spaces;

namespace Yello.Infrastructure.Persistence;

/// <summary>
/// Counts and records live presence in the resolved Space, inside the request's unit of work.
/// NFR-8 / AD-25.
/// </summary>
/// <remarks>
/// <para>
/// <b>On the request's own context, in its transaction, under its session context.</b> The pipeline
/// calls this after the Membership is confirmed, so both isolation layers are already narrowed to the
/// Space: the queries below need no <c>Where</c> on <c>SpaceId</c>, and the BLOCK predicate admits
/// the insert. A refusal leaves nothing written, because nothing has been written yet when it is
/// decided and the pipeline rolls the unit back.
/// </para>
/// <para>
/// <b>Expiry is read, never written by a timer</b> (AD-27). A presence counts while
/// <c>ExpiresAt</c> is in the future, evaluated in the counting statement itself; an expired row is
/// simply not counted, and is reused when its Account returns.
/// </para>
/// <para>
/// <b>Write amplification, bounded.</b> Every resolved request could rewrite its row; instead the
/// row is rewritten only once less than half its <see cref="Window"/> remains, so a busy Account
/// writes about twice per window rather than once per request.
/// </para>
/// <para>
/// <b>Known limit: two newcomers racing at the bound can both be admitted.</b> The count and the
/// insert run under the default isolation level, so two transactions that each count 49 can each
/// insert a 50th. The overshoot is bounded by concurrency at that instant and is recorded in the
/// story's completion notes rather than closed with a range lock on every request.
/// </para>
/// </remarks>
/// <param name="dbContext">The request's unit of work.</param>
/// <param name="time">The clock.</param>
internal sealed class SpacePresenceRegister(YelloDbContext dbContext, TimeProvider time) : ISpacePresenceRegister
{
    /// <summary>
    /// How long a presence counts after the Account's last request in the Space. Fifteen minutes,
    /// the horizon over which the database itself treats a quiet workload as idle.
    /// </summary>
    internal static TimeSpan Window { get; } = TimeSpan.FromMinutes(15);

    /// <summary>
    /// A present Account's row is rewritten only once less than this remains: half the window.
    /// </summary>
    internal static TimeSpan RefreshBelow { get; } = TimeSpan.FromMinutes(7.5);

    /// <inheritdoc />
    public async Task<bool> TryEnterAsync(Guid accountId, Guid spaceId, int limit, CancellationToken cancellationToken)
    {
        var now = time.GetUtcNow();

        var own = await dbContext.SpacePresences
            .SingleOrDefaultAsync(presence => presence.AccountId == accountId, cancellationToken)
            .ConfigureAwait(false);

        if (own is null || own.ExpiresAt <= now)
        {
            var live = await dbContext.SpacePresences
                .CountAsync(presence => presence.ExpiresAt > now, cancellationToken)
                .ConfigureAwait(false);

            if (live >= limit)
            {
                return false;
            }
        }

        if (own is null)
        {
            dbContext.SpacePresences.Add(new SpacePresence
            {
                SpaceId = spaceId,
                AccountId = accountId,
                ExpiresAt = now + Window,
            });
        }
        else if (own.ExpiresAt - now < RefreshBelow)
        {
            own.ExpiresAt = now + Window;
        }
        else
        {
            // Present, and with more than half the window left: nothing to write.
            return true;
        }

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return true;
    }
}
