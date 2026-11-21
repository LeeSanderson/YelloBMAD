using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Yello.Application.Spaces;
using Yello.Domain.AccessRefusals;

namespace Yello.Infrastructure.Persistence;

/// <summary>
/// Deletes <see cref="AccessRefusal"/> rows older than the retention period, when request traffic
/// arrives and a purge is due. AD-20 / AR-11; AD-10 / AR-33.
/// </summary>
/// <remarks>
/// <para>
/// <b>90 days is PRD section 12 assumption 11</b> (<c>prd.md:902</c>, <c>SPEC.md:277</c>) - hardened
/// and unconfirmed - so it is one named value here and its test carries the assumption's trait.
/// </para>
/// <para>
/// <b>At most once a day per process, decided by <see cref="RefusalPurgeSchedule"/>.</b> The first
/// request a process serves claims the purge; every later call within the day returns a completed
/// task without touching the database. Nothing here runs unless a request has already arrived, so
/// an idle database is never woken by it.
/// </para>
/// <para>
/// <b>A singleton, and a context of its own in a scope of its own.</b> The Host runs the purge off
/// the request path, so it must not borrow anything the request owns - the request's scope may be
/// gone before the delete finishes.
/// </para>
/// </remarks>
/// <param name="scopes">Creates the scope the purge's context lives in.</param>
/// <param name="schedule">The process's record of when it last purged.</param>
/// <param name="time">The clock.</param>
internal sealed class AccessRefusalPurge(
    IServiceScopeFactory scopes,
    RefusalPurgeSchedule schedule,
    TimeProvider time) : IRefusalRetention
{
    /// <summary>
    /// How long a refusal record is kept. PRD section 12, assumption 11.
    /// </summary>
    internal static TimeSpan Retention { get; } = TimeSpan.FromDays(90);

    /// <summary>
    /// Deletes every refusal recorded against one Account, ahead of its retention: the Account is being erased, and a
    /// record that outlived it would keep its id.
    /// </summary>
    /// <param name="dbContext">The Account deletion's erase, inside its transaction.</param>
    /// <param name="accountId">The Account being erased.</param>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    /// <returns>A task.</returns>
    internal static Task EraseAccountAsync(YelloDbContext dbContext, Guid accountId, CancellationToken cancellationToken) =>
        dbContext.Set<AccessRefusal>()
            .Where(refusal => refusal.AccountId == accountId)
            .ExecuteDeleteAsync(cancellationToken);

    /// <inheritdoc />
    public Task PurgeIfDueAsync(CancellationToken cancellationToken)
    {
        var now = time.GetUtcNow();

        return schedule.TryClaim(now)
            ? PurgeAsync(now - Retention, cancellationToken)
            : Task.CompletedTask;
    }

    private async Task PurgeAsync(DateTimeOffset cutoff, CancellationToken cancellationToken)
    {
        var scope = scopes.CreateAsyncScope();

        await using (scope.ConfigureAwait(false))
        {
            var options = scope.ServiceProvider.GetRequiredService<DbContextOptions<YelloDbContext>>();
            var context = new YelloDbContext(options);

            await using (context.ConfigureAwait(false))
            {
                await context.Set<AccessRefusal>()
                    .Where(refusal => refusal.OccurredAt < cutoff)
                    .ExecuteDeleteAsync(cancellationToken)
                    .ConfigureAwait(false);
            }
        }
    }
}
