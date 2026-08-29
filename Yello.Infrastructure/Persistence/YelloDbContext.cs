using Microsoft.EntityFrameworkCore;
using Yello.Domain.Accounts;
using Yello.Domain.Memberships;
using Yello.Domain.Spaces;
using Yello.Domain.Statuses;
using Yello.Infrastructure.Persistence.Configurations;

namespace Yello.Infrastructure.Persistence;

/// <summary>
/// The solution's only <see cref="DbContext"/>, and the first schema in the repository.
/// </summary>
/// <remarks>
/// <para>
/// <b>No migration is applied from here, ever.</b> AR-36 (<c>epics.md:206</c>) forbids running
/// migrations at startup; story 1.10 applies them as an explicit deploy step. There is no
/// <c>Database.Migrate()</c>, no <c>EnsureCreated()</c> and no hosted service that would reach
/// one - and <c>StartupConnectivityCheck</c>'s own remarks already record AR-36 as the reason it
/// opens a connection and does nothing else.
/// </para>
/// <para>
/// <b>The entities carry no navigation properties, deliberately.</b> Relationships are declared
/// here through the foreign-key properties the entities do have. A navigation from
/// <c>Space</c> to its Memberships would be a convenience this story does not need and a lazy
/// path later stories would load without meaning to - and the entities live in
/// <c>Yello.Domain</c>, whose job is invariants rather than a queryable object graph.
/// </para>
/// <para>
/// <b>What is deliberately NOT here, and belongs to story 1.5:</b> the global query filters that
/// form row-level security's independent second layer, <c>ActiveSpaceContext</c> and the
/// per-request session-context wiring, <c>MAXDOP = 1</c>, and the pooled-connection reuse case.
/// This story writes the database-side policies (see the migration) and the one place that sets
/// the session context - <see cref="AccountRegistrationStore"/> - because registration is
/// unauthenticated and has no request-scoped Space to resolve.
/// </para>
/// </remarks>
/// <param name="options">Provider and connection configuration, supplied by DI.</param>
public sealed class YelloDbContext(DbContextOptions<YelloDbContext> options) : DbContext(options)
{
    /// <summary>
    /// Accounts. One row per registered email address.
    /// </summary>
    public DbSet<Account> Accounts => Set<Account>();

    /// <summary>
    /// Spaces. There is one kind, and this set holds all of them.
    /// </summary>
    public DbSet<Space> Spaces => Set<Space>();

    /// <summary>
    /// Memberships - the Account-in-a-Space join that carries the Role.
    /// </summary>
    public DbSet<Membership> Memberships => Set<Membership>();

    /// <summary>
    /// Status definitions, per Space.
    /// </summary>
    public DbSet<StatusDefinition> StatusDefinitions => Set<StatusDefinition>();

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Named one by one rather than found by assembly scan, and the reason is worth keeping.
        // `ApplyConfigurationsFromAssembly` was the first shape here: shorter, and it cannot go
        // stale. It also means nothing in the solution statically references any configuration
        // class, which MA0182 reported accurately - "internal type is apparently never used" is
        // exactly true of a type only reflection reaches, and a configuration deleted by mistake
        // would then produce a silently different schema rather than a compile error.
        //
        // The risk an explicit list carries is the opposite one: a later story adds a
        // configuration and forgets to add it here, and the entity quietly takes EF Core's
        // conventions instead - no explicit table name, no row-level security column mapping, no
        // filtered index. That is not left to discipline.
        // EntityConfigurationCompletenessTests asserts that every IEntityTypeConfiguration<T> in
        // this assembly reaches the built model, so the list below cannot fall behind the
        // directory beside it.
        modelBuilder.ApplyConfiguration(new AccountConfiguration());
        modelBuilder.ApplyConfiguration(new SpaceConfiguration());
        modelBuilder.ApplyConfiguration(new MembershipConfiguration());
        modelBuilder.ApplyConfiguration(new StatusDefinitionConfiguration());

        base.OnModelCreating(modelBuilder);
    }
}
