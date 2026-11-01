using Yello.Application.Outbox;
using Yello.Domain;
using Yello.Infrastructure.Persistence;

namespace Yello.Infrastructure.Outbox;

/// <summary>
/// Enqueues an email in the request's own unit of work. AD-10 / AR-33; story 4.1, AC6.
/// </summary>
/// <remarks>
/// <para>
/// <b>Same transaction as the triggering write.</b> The row is saved on the request's context, inside the transaction
/// the pipeline opened, so a write that rolls back takes its email with it. It saves its own change before the pipeline
/// records the idempotency key (which refuses to save while anything else is pending).
/// </para>
/// <para>
/// <b>Sent only after the commit.</b> It marks the request's <see cref="OutboxCommitSignal"/>; the unit of work kicks the
/// dispatcher once its commit has succeeded, and never on a rollback.
/// </para>
/// </remarks>
/// <param name="dbContext">The request's unit of work.</param>
/// <param name="identifiers">Produces the row's identity (AR-34).</param>
/// <param name="clock">Stamps the enqueue instant.</param>
/// <param name="signal">The request's post-commit signal.</param>
internal sealed class OutboxStore(
    YelloDbContext dbContext,
    IIdentifierGenerator identifiers,
    TimeProvider clock,
    OutboxCommitSignal signal) : IOutbox
{
    /// <inheritdoc />
    public Task EnqueueAsync(OutboxEmail email, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(email);

        return EnqueueCoreAsync(email, cancellationToken);
    }

    private async Task EnqueueCoreAsync(OutboxEmail email, CancellationToken cancellationToken)
    {
        dbContext.Set<OutboxMessage>().Add(new OutboxMessage
        {
            Id = identifiers.Next(),
            Kind = email.Kind,
            Recipient = email.Recipient,
            Subject = email.Subject,
            Body = email.Body,
            EnqueuedAt = clock.GetUtcNow(),
        });

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        signal.MarkEnqueued();
    }
}
