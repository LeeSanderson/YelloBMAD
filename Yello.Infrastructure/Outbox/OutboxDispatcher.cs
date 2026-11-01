using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Yello.Application.Outbox;
using Yello.Infrastructure.Persistence;

namespace Yello.Infrastructure.Outbox;

/// <summary>
/// Sends what the outbox holds, in process and off the request path: after a commit that enqueued, at process start, and
/// piggybacked on inbound authenticated traffic at most once a minute. AD-10 / AR-33, A-9; story 4.1, AC6.
/// </summary>
/// <remarks>
/// <para>
/// <b>Never a timer, a <c>BackgroundService</c> or an <c>IHostedService</c></b> - the A-9 gate bans all three. Every
/// run is started by something that already happened: a commit, the process starting, or a request that has already
/// read the database. An idle database is never woken.
/// </para>
/// <para>
/// <b>A singleton with a context of its own in a scope of its own</b> (the <c>AccessRefusalPurge</c> precedent): a run
/// outlives the request that kicked it. Its context sets <b>no</b> session key, which is why the outbox carries no
/// policy (Lee's Q6).
/// </para>
/// <para>
/// <b>Each row is claimed by one guarded statement against the database clock</b>:
/// <c>UPDATE TOP (n) ... WITH (READPAST) SET ClaimedAt = SYSDATETIMEOFFSET(), Attempts += 1 OUTPUT ...</c> over rows
/// never claimed or claimed more than five minutes ago. Two dispatchers - the post-commit kick and a sweep, or two
/// replicas - never claim the same row, and a row whose claimant died is claimed again once its claim is stale. Each
/// claimed row is sent and then deleted. <b>Delivery is at least once</b>: a crash between the send and the delete sends
/// it again.
/// </para>
/// <para>
/// <b>One run at a time per process.</b> A kick that arrives during a run asks for another pass rather than starting a
/// second run, so nothing enqueued is left behind and no two runs race in one process.
/// </para>
/// </remarks>
/// <param name="scopes">Creates the scope a run's context and sender live in.</param>
/// <param name="schedule">The piggyback trigger's throttle.</param>
/// <param name="time">The clock the throttle reads.</param>
/// <param name="logger">The outbox's log.</param>
internal sealed class OutboxDispatcher(
    IServiceScopeFactory scopes,
    OutboxSweepSchedule schedule,
    TimeProvider time,
    ILogger<OutboxDispatcher> logger) : IOutboxRecovery
{
    /// <summary>
    /// How many rows one claim takes.
    /// </summary>
    private const int ClaimBatch = 10;

    /// <summary>
    /// The claim at which a message that still fails is deleted unsent (Lee's decision D2 (a)). <c>Attempts</c> counts
    /// claims, each followed by one send.
    /// </summary>
    internal const int MaxAttempts = 10;

    private int _running;

    private int _requested;

    /// <inheritdoc />
    public void SweepNow()
    {
        Interlocked.Exchange(ref _requested, 1);

        if (Interlocked.CompareExchange(ref _running, 1, 0) == 0)
        {
            _ = Task.Run(RunAsync);
        }
    }

    /// <inheritdoc />
    public bool SweepIfDue()
    {
        if (!schedule.TryClaim(time.GetUtcNow()))
        {
            return false;
        }

        SweepNow();

        return true;
    }

    private async Task RunAsync()
    {
        try
        {
            while (Interlocked.Exchange(ref _requested, 0) == 1)
            {
                await DrainAsync().ConfigureAwait(false);
            }
        }
        // S2221 disabled here, deliberately and narrowly (the StartupConnectivityCheck precedent): a fire-and-forget run
        // has no caller to observe what an allow-list of exception types omits, so the list itself would be the defect.
#pragma warning disable S2221 // "Catch a list of specific exception subtype or use exception filters"
        catch (Exception exception) when (exception is not OutOfMemoryException)
#pragma warning restore S2221
        {
            // Off any request path, nothing above this frame would observe it, so every failure is logged here rather than
            // left as an unobserved task exception (story 4.1 code review). A Host with no database configured lands here
            // too, no louder.
            OutboxLog.SweepFailed(logger, exception);
        }
        finally
        {
            Volatile.Write(ref _running, 0);
        }

        // A kick that arrived after the last pass looked, and found the run still marked as running.
        if (Volatile.Read(ref _requested) == 1)
        {
            SweepNow();
        }
    }

    private async Task DrainAsync()
    {
        var scope = scopes.CreateAsyncScope();

        await using (scope.ConfigureAwait(false))
        {
            var sender = scope.ServiceProvider.GetRequiredService<IEmailSender>();
            var options = scope.ServiceProvider.GetRequiredService<DbContextOptions<YelloDbContext>>();
            var context = new YelloDbContext(options);

            await using (context.ConfigureAwait(false))
            {
                // A pass in which anything failed is the run's last (story 4.1 code review): its failed rows stay claimed
                // for five minutes, so a run that went on claiming would, with the provider down and enough rows queued,
                // meet its own failures again once their claims went stale and never end. The rest waits for the next
                // commit, sweep or process start - which is the back-off.
                var failed = false;

                while (!failed)
                {
                    var claimed = await ClaimAsync(context).ConfigureAwait(false);

                    if (claimed.Count == 0)
                    {
                        return;
                    }

                    foreach (var message in claimed)
                    {
                        failed |= !await SendAsync(context, sender, message).ConfigureAwait(false);
                    }
                }
            }
        }
    }

    /// <summary>
    /// Re-stamps one claimed row's claim just before it is sent, guarded on the claim this run took, so a row late in a
    /// slow batch is never past its five minutes while it is being sent and cannot be claimed and sent by a second
    /// dispatcher at the same time (story 4.1 code review). <c>false</c> when another claimant has taken it since.
    /// </summary>
    private static async Task<bool> RenewClaimAsync(YelloDbContext context, OutboxMessage message) =>
        await context.Database.ExecuteSqlAsync(
            $"""
            UPDATE dbo.[OutboxMessage] WITH (ROWLOCK)
            SET ClaimedAt = SYSDATETIMEOFFSET()
            WHERE Id = {message.Id} AND ClaimedAt = {message.ClaimedAt}
            """,
            CancellationToken.None).ConfigureAwait(false) == 1;

    /// <summary>
    /// Claims up to <see cref="ClaimBatch"/> rows in one statement, by the database clock, skipping rows another
    /// claimant holds locked.
    /// </summary>
    private static async Task<List<OutboxMessage>> ClaimAsync(YelloDbContext context) =>
        await context.Set<OutboxMessage>()
            .FromSql($"""
                UPDATE TOP ({ClaimBatch}) dbo.[OutboxMessage] WITH (READPAST, ROWLOCK)
                SET ClaimedAt = SYSDATETIMEOFFSET(), Attempts = Attempts + 1
                OUTPUT inserted.Id, inserted.Kind, inserted.Recipient, inserted.Subject, inserted.Body, inserted.EnqueuedAt, inserted.ClaimedAt, inserted.Attempts
                WHERE ClaimedAt IS NULL OR ClaimedAt < DATEADD(minute, -5, SYSDATETIMEOFFSET())
                """)
            .AsNoTracking()
            .ToListAsync(CancellationToken.None)
            .ConfigureAwait(false);

    /// <summary>
    /// Sends one claimed row and deletes it; or, when the provider refuses it for good or it has been tried
    /// <see cref="MaxAttempts"/> times, deletes it unsent (Lee's decision D2 (a), story 4.1 code review). Every failure is
    /// caught here, so one bad row never stops the rest of its batch.
    /// </summary>
    /// <returns><c>false</c> when the row is left for a later sweep, so the run ends after this pass.</returns>
    private async Task<bool> SendAsync(YelloDbContext context, IEmailSender sender, OutboxMessage message)
    {
        if (!await RenewClaimAsync(context, message).ConfigureAwait(false))
        {
            // Another claimant took it after its claim went stale: theirs to send.
            return true;
        }

        try
        {
            await sender.SendAsync(
                new OutboxEmail(message.Kind, message.Recipient, message.Subject, message.Body),
                CancellationToken.None).ConfigureAwait(false);
        }
        // S2221 disabled for the same reason as in RunAsync: one row's unforeseen failure must not end its batch.
#pragma warning disable S2221 // "Catch a list of specific exception subtype or use exception filters"
        catch (Exception exception) when (exception is not OutOfMemoryException)
#pragma warning restore S2221
        {
            if (exception is EmailNotSentException { IsPermanent: true } || message.Attempts >= MaxAttempts)
            {
                // Nothing about an undeliverable message - its address, its content, its token - outlives the decision
                // to stop trying (Q7). Logged by id and attempt count only.
                await DeleteAsync(context, message).ConfigureAwait(false);
                OutboxLog.Abandoned(logger, message.Id, message.Attempts, exception);

                // Settled - nothing of it is left to meet again - so it does not end the run.
                return true;
            }

            // Left claimed: claimed again once the claim is five minutes stale, by a later sweep.
            OutboxLog.NotSent(logger, message.Id, exception);

            return false;
        }

        try
        {
            await DeleteAsync(context, message).ConfigureAwait(false);
        }
        catch (DbException exception)
        {
            // Sent, but still in the outbox: it is sent again once its claim is stale (at least once, as recorded).
            OutboxLog.SentButNotDeleted(logger, message.Id, exception);

            return false;
        }

        OutboxLog.Sent(logger, message.Id);

        return true;
    }

    private static Task<int> DeleteAsync(YelloDbContext context, OutboxMessage message) =>
        context.Set<OutboxMessage>()
            .Where(row => row.Id == message.Id)
            .ExecuteDeleteAsync(CancellationToken.None);
}
