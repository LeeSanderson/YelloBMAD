using Microsoft.EntityFrameworkCore;

namespace Yello.Infrastructure.Persistence.Configurations;

/// <summary>
/// Story 4.1's part of the model: the Invitation table, the outbox, and the database clock the shared actionable
/// predicate compares against (AD-27); and story 4.3's policy-free token index (Lee's Q1 (a)). Named one by one, for the
/// reason <see cref="YelloDbContext"/> gives.
/// </summary>
internal static class InvitationModel
{
    /// <summary>
    /// Applies the configurations and maps the database clock.
    /// </summary>
    /// <param name="modelBuilder">The model.</param>
    /// <param name="context">The context whose request-scoped Space the Invitation query filter closes over.</param>
    internal static void Apply(ModelBuilder modelBuilder, YelloDbContext context)
    {
        modelBuilder.ApplyConfiguration(new InvitationConfiguration(context));
        modelBuilder.ApplyConfiguration(new InvitationTokenConfiguration());
        modelBuilder.ApplyConfiguration(new OutboxMessageConfiguration());

        DatabaseClock.Map(modelBuilder);
    }
}
