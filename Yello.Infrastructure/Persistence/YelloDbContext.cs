using Microsoft.EntityFrameworkCore;
using Yello.Domain.Accounts;
using Yello.Domain.Memberships;
using Yello.Domain.Projects;
using Yello.Domain.Spaces;
using Yello.Domain.Statuses;
using Yello.Domain.Tasks;
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
/// <b>Two independent layers of Space isolation meet here.</b> The database's row-level security
/// policy filters on <c>SESSION_CONTEXT('SpaceId')</c>, which
/// <see cref="SpaceSessionContext"/> sets at the start of every unit of work. Every Space-scoped
/// entity also carries an EF Core global query filter on <see cref="ScopedSpaceId"/>, which is
/// the request's <see cref="SpaceScope"/> - application state, never read from the session
/// context - so either layer alone still excludes another Space's rows (AD-2, AC4).
/// <c>Account</c> and <c>Space</c> carry neither: the spine states no predicate for them, and
/// choosing one belongs to the AD-24 amendment due before Epic 3.
/// </para>
/// <para>
/// <b>One table is readable across Spaces, and only by Account.</b> Since story 1.7,
/// <c>Membership</c>'s policy and filter both admit a row by its Space <i>or</i> by its Account
/// (<c>SESSION_CONTEXT('AccountId')</c> in the database, <see cref="ScopedAccountId"/> here), which is
/// how the Space switcher lists an Account's Spaces. A unit of work sets exactly one of the two keys,
/// so a Space-scoped request still sees one Space's Memberships and nothing more. <c>Space</c>'s
/// names reach the switcher only through a join from that read - see
/// <c>AccountSpaceDirectory</c>.
/// </para>
/// </remarks>
/// <param name="options">Provider and connection configuration, supplied by DI.</param>
/// <param name="spaceScope">
/// The request's Space scope. Optional so that a context built outside a request - the design-time
/// factory, a test - still constructs; such a context has no Space in scope and its query filters
/// match no Space-scoped row.
/// </param>
/// <param name="accountScope">
/// The request's Account scope, which only <c>Membership</c>'s filter reads (story 1.7, AD-24).
/// Optional for the same reason, and fails closed the same way.
/// </param>
public sealed class YelloDbContext(
    DbContextOptions<YelloDbContext> options,
    SpaceScope? spaceScope = null,
    AccountScope? accountScope = null)
    : DbContext(options)
{
    private readonly SpaceScope _spaceScope = spaceScope ?? new SpaceScope();

    private readonly AccountScope _accountScope = accountScope ?? new AccountScope();

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

    /// <summary>
    /// Live presences, per Space - what NFR-8's concurrent-active-Sessions bound counts.
    /// </summary>
    /// <remarks>
    /// The <c>AccessRefusal</c> record has no set here, deliberately: it is write-only, and only its
    /// recorder and its purge reach it, through <c>Set&lt;T&gt;()</c>.
    /// </remarks>
    public DbSet<SpacePresence> SpacePresences => Set<SpacePresence>();

    /// <summary>
    /// Projects, per Space (story 2.1).
    /// </summary>
    public DbSet<Project> Projects => Set<Project>();

    /// <summary>
    /// Tasks, per Space (story 2.2). The CLR type is <see cref="TaskItem"/>; the table is <c>Task</c>.
    /// </summary>
    public DbSet<TaskItem> Tasks => Set<TaskItem>();

    /// <summary>
    /// The Space every global query filter compares against, read from this context's
    /// <see cref="SpaceScope"/> each time a query runs.
    /// </summary>
    /// <remarks>
    /// A property of the context rather than a captured value, because EF Core builds the model
    /// once per process and re-evaluates a context member on every query - so each request's
    /// filters see that request's scope, never the first one's.
    /// </remarks>
    internal Guid? ScopedSpaceId => _spaceScope.SpaceId;

    /// <summary>
    /// The Account <c>Membership</c>'s filter admits alongside <see cref="ScopedSpaceId"/>, read from
    /// this context's <see cref="AccountScope"/> each time a query runs. <c>null</c> - matching
    /// nothing - except inside the Account-scoped adapter's unit of work.
    /// </summary>
    internal Guid? ScopedAccountId => _accountScope.AccountId;

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
        // PersistenceModelGateTests.Every_entity_configuration_reaches_the_model asserts that
        // every IEntityTypeConfiguration<T> in this assembly reaches the built model, so the list
        // below cannot fall behind the directory beside it.
        //
        // The Space-scoped configurations take this context so their query filters can close over
        // ScopedSpaceId. PersistenceModelGateTests.Every_Space_scoped_entity_has_a_query_filter_from_application_state
        // fails the build on one that does not.
        modelBuilder.ApplyConfiguration(new AccountConfiguration());
        modelBuilder.ApplyConfiguration(new SpaceConfiguration());
        modelBuilder.ApplyConfiguration(new MembershipConfiguration(this));
        modelBuilder.ApplyConfiguration(new StatusDefinitionConfiguration(this));
        modelBuilder.ApplyConfiguration(new SpacePresenceConfiguration(this));
        modelBuilder.ApplyConfiguration(new ProjectConfiguration(this));
        modelBuilder.ApplyConfiguration(new TaskConfiguration(this));
        modelBuilder.ApplyConfiguration(new IdempotencyRecordConfiguration(this));
        modelBuilder.ApplyConfiguration(new AccessRefusalConfiguration());
        modelBuilder.ApplyConfiguration(new ApiTokenConfiguration());
        modelBuilder.ApplyConfiguration(new DataProtectionKeyConfiguration());

        base.OnModelCreating(modelBuilder);
    }
}
