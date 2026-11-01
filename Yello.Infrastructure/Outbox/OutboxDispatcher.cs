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
        catch (Exception exception) when (exception is DbException or InvalidOperationException or OperationCanceledException)
        {
            // Off any request path, nothing above this frame would observe it. A Host with no database configured lands
            // here too, no louder.
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
                while (true)
                {
                    var claimed = await ClaimAsync(context).ConfigureAwait(false);

                    if (claimed.Count == 0)
                    {
                        return;
                    }

                    foreach (var message in claimed)
                    {
                        await SendAsync(context, sender, message).ConfigureAwait(false);
                    }
                }
            }
        }
    }

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

    private async Task SendAsync(YelloDbContext context, IEmailSender sender, OutboxMessage message)
    {
        try
        {
            await sender.SendAsync(
                new OutboxEmail(message.Kind, message.Recipient, message.Subject, message.Body),
                CancellationToken.None).ConfigureAwait(false);
        }
        catch (EmailNotSentException exception)
        {
            // Left claimed: claimed again once the claim is five minutes stale, by the next sweep.
            OutboxLog.NotSent(logger, message.Id, exception);

            return;
        }

        await context.Set<OutboxMessage>()
            .Where(sent => sent.Id == message.Id)
            .ExecuteDeleteAsync(CancellationToken.None)
            .ConfigureAwait(false);

        OutboxLog.Sent(logger, message.Id);
    }
}
