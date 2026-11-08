using Microsoft.EntityFrameworkCore;

namespace Yello.Infrastructure.Persistence.Configurations;

/// <summary>
/// The offers in the model: the Invitation table, the outbox, and the database clock the shared actionable predicate
/// compares against; the policy-free token index; the notification send record the outbox store writes beside each email;
/// and the Ownership Offer table. Named one by one, for the reason <see cref="YelloDbContext"/> gives.
/// </summary>
internal static class InvitationModel
{
    /// <summary>
    /// Applies the configurations and maps the database clock.
    /// </summary>
    /// <param name="modelBuilder">The model.</param>
    /// <param name="context">The context whose request-scoped Space the Space-scoped query filters close over.</param>
    internal static void Apply(ModelBuilder modelBuilder, YelloDbContext context)
    {
        modelBuilder.ApplyConfiguration(new InvitationConfiguration(context));
        modelBuilder.ApplyConfiguration(new InvitationTokenConfiguration());
        modelBuilder.ApplyConfiguration(new OutboxMessageConfiguration());
        modelBuilder.ApplyConfiguration(new NotificationSendConfiguration(context));
        modelBuilder.ApplyConfiguration(new OwnershipOfferConfiguration(context));

        DatabaseClock.Map(modelBuilder);
    }
}
