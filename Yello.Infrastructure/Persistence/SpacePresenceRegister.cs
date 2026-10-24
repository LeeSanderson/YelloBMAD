using Microsoft.Data.SqlClient;
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
    /// SQL Server's "violation of PRIMARY KEY constraint" - the same number as a UNIQUE constraint.
    /// </summary>
    private const int PrimaryKeyViolationErrorNumber = 2627;

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
            return await EnterAsync(new SpacePresence
            {
                SpaceId = spaceId,
                AccountId = accountId,
                ExpiresAt = now + Window,
            }, cancellationToken).ConfigureAwait(false);
        }

        if (own.ExpiresAt - now >= RefreshBelow)
        {
            // Present, and with more than half the window left: nothing to write.
            return true;
        }

        own.ExpiresAt = now + Window;

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return true;
    }

    /// <summary>
    /// True when a failed save was a primary-key violation. <c>(SpaceId, AccountId)</c> is the
    /// table's only key and it has no unique index, so on this table that can mean one thing: the
    /// same Account's row, inserted by a concurrent request.
    /// </summary>
    /// <remarks>
    /// Matched on the error number, never on the message, which the server localises - the same
    /// rule <see cref="AccountRegistrationStore"/> follows.
    /// </remarks>
    private static bool IsPrimaryKeyViolation(DbUpdateException exception) =>
        exception.InnerException is SqlException sqlException
        && sqlException.Errors.Cast<SqlError>().Any(error => error.Number == PrimaryKeyViolationErrorNumber);

    /// <summary>
    /// Inserts the Account's first presence in the Space.
    /// </summary>
    /// <remarks>
    /// <b>The same Account entering twice at once is admitted, not answered 500.</b> Two requests
    /// from one Account (two tabs, or a page that fires its first calls in parallel) can both find no
    /// row. The second insert waits on the first's key lock. If the first commits, the second fails
    /// on the primary key, and that is the Account already present: admitted. A primary-key violation
    /// ends only the statement, not the transaction, so the request's unit of work carries on. The
    /// failed entity is detached so the endpoint's own save does not try the insert again.
    /// </remarks>
    private async Task<bool> EnterAsync(SpacePresence presence, CancellationToken cancellationToken)
    {
        dbContext.SpacePresences.Add(presence);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateException exception) when (IsPrimaryKeyViolation(exception))
        {
            dbContext.Entry(presence).State = EntityState.Detached;
        }

        return true;
    }
}
