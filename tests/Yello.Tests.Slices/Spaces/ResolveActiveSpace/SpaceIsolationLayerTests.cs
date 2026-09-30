using System.Data;
using System.Globalization;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Xunit;
using Yello.Domain.Accounts;
using Yello.Domain.Memberships;
using Yello.Infrastructure.Persistence;
using Yello.Tests.Slices.Accounts.RegisterAccount;

namespace Yello.Tests.Slices.Spaces.ResolveActiveSpace;

/// <summary>
/// Story 1.5's two isolation layers, the Membership read that resolves the active Space, the
/// database configuration, and the SQL Server semantics the whole design rests on.
/// </summary>
/// <remarks>
/// <para>
/// <b>One class, one container.</b> Cost is per container-backed class rather than per case
/// (<c>TESTING-CONVENTIONS.md:317-332</c>), and the headroom on this machine was measured as gone
/// on 2026-09-18. The pooled-connection case is the one exception and lives in its own class,
/// because it needs a pool pinned to one connection and cannot observe anything on a shared pool.
/// </para>
/// <para>
/// <b>Two Spaces, always, with different ids.</b> The framework run found a plant that passed
/// because it compared <c>"en"</c> with <c>"en"</c> - a value against a default. A filter test
/// where the ambient <c>SpaceId</c> happens to equal the row's <c>SpaceId</c> is the same shape and
/// asserts nothing, so every isolation assertion here reads a named row from one Space while the
/// other Space's row exists and would be visible if the layer under test failed.
/// </para>
/// <para>
/// <b>Seeded by B2's first shape</b> - through the application, one Space per unit of work - via
/// <see cref="SpaceSeeding"/>.
/// </para>
/// </remarks>
[Trait("Suite", "Slices")]
[Trait("Priority", "P0")]
[Trait("Requirement", "FR-15")]
[Trait("Requirement", "AR-5")]
[Trait("Requirement", "AR-6")]
[Trait("Requirement", "AR-7")]
[Trait("Requirement", "AR-8")]
[Trait("Requirement", "AD-1")]
[Trait("Requirement", "AD-2")]
[Trait("Requirement", "NFR-1")]
public sealed class SpaceIsolationLayerTests(MigratedDatabaseFixture fixture)
    : IClassFixture<MigratedDatabaseFixture>
{
    /// <summary>
    /// The serial-plan setting AD-2 requires, removing the SESSION_CONTEXT parallel-plan defect.
    /// </summary>
    private const int SerialPlanDegreeOfParallelism = 1;

    /// <summary>
    /// SQL Server's error number for a session-context key already set <c>@read_only = 1</c>.
    /// Asserted rather than assumed - see the empirical case below.
    /// </summary>
    private const int ReadOnlySessionKeyErrorNumber = 15664;

    /// <summary>
    /// AC4 - the EF Core query filter excludes another Space's rows on its own.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>This is the case that makes AC4 mean anything.</b> AD-2 (<c>ARCHITECTURE-SPINE.md:84</c>)
    /// requires the filter to be "derived from application state rather than session context, so
    /// neither layer alone carries NFR-1". A test run with both layers agreeing cannot tell which
    /// one excluded the row, and would pass identically against an implementation with no filter
    /// at all.
    /// </para>
    /// <para>
    /// So the two layers are deliberately set to disagree: the session context names Space B, which
    /// row-level security therefore admits, while the application state names Space A. Only the
    /// application-state layer can exclude Space B's row. A filter that secretly read
    /// <c>SESSION_CONTEXT()</c> - or no filter at all - returns it and fails. The same connection
    /// with the filter disarmed is read first, so the exclusion is shown to be the filter's rather
    /// than an absent row's.
    /// </para>
    /// </remarks>
    [Fact]
    public async Task The_query_filter_alone_excludes_another_Spaces_rows()
    {
        var database = Available();
        var (spaceA, spaceB) = await SeedTwoSpacesAsync(database);

        var scope = new SpaceScope();
        scope.Narrow(spaceA);

        await using var context = database.CreateContext(scope);
        await using var transaction = await context.Database.BeginTransactionAsync(TestContext.Current.CancellationToken);

        await SpaceSessionContext.SetAsync(context.Database, spaceB, TestContext.Current.CancellationToken);

        var admittedByRowLevelSecurity = await context.StatusDefinitions
            .IgnoreQueryFilters()
            .Select(status => status.Name)
            .ToListAsync(TestContext.Current.CancellationToken);

        Assert.Equal(["Beta"], admittedByRowLevelSecurity);

        var visible = await context.StatusDefinitions
            .Select(status => status.Name)
            .ToListAsync(TestContext.Current.CancellationToken);

        Assert.Empty(visible);
    }

    /// <summary>
    /// AC4 - row-level security excludes another Space's rows on its own.
    /// </summary>
    /// <remarks>
    /// The other half of independence. <c>IgnoreQueryFilters()</c> removes the application-state
    /// layer, and the application state is set to Space B besides, so nothing but the policy stands
    /// between this read and Space B's row. The session context names Space A, and only Space A's
    /// row may come back. Distinct from
    /// <see cref="RegisterAccountIntegrationTests.Space_scoped_rows_are_invisible_without_a_session_context"/>,
    /// which proves the policy works at all - this proves it still works with the second layer
    /// deliberately disarmed and pointed the wrong way.
    /// </remarks>
    [Fact]
    public async Task Row_level_security_alone_excludes_another_Spaces_rows()
    {
        var database = Available();
        var (spaceA, spaceB) = await SeedTwoSpacesAsync(database);

        var scope = new SpaceScope();
        scope.Narrow(spaceB);

        await using var context = database.CreateContext(scope);
        await using var transaction = await context.Database.BeginTransactionAsync(TestContext.Current.CancellationToken);

        await SpaceSessionContext.SetAsync(context.Database, spaceA, TestContext.Current.CancellationToken);

        var visible = await context.StatusDefinitions
            .IgnoreQueryFilters()
            .Select(status => status.Name)
            .ToListAsync(TestContext.Current.CancellationToken);

        Assert.Equal(["Alpha"], visible);
    }

    /// <summary>
    /// AC1 and AC7 - a Membership in the requested Space resolves a context carrying that
    /// Membership's Role.
    /// </summary>
    [Fact]
    public async Task A_Membership_in_the_requested_Space_resolves_a_context_carrying_its_Role()
    {
        var database = Available();
        var account = SpaceSeeding.NewAccount();
        var spaceId = Guid.NewGuid();

        await SpaceSeeding.SeedAsync(database, spaceId, context =>
        {
            context.Accounts.Add(account);
            context.Spaces.Add(SpaceSeeding.NewSpace(spaceId));
            context.Memberships.Add(SpaceSeeding.NewMembership(spaceId, account.Id, Role.Member));
        }, TestContext.Current.CancellationToken);

        var (context, scope) = Request(database);

        await using (context)
        {
            await using var unitOfWork = await new MembershipActiveSpaceResolver(context, scope)
                .BeginAsync(account.Id, spaceId, TestContext.Current.CancellationToken);

            Assert.NotNull(unitOfWork);
            Assert.Equal(account.Id, unitOfWork.Context.AccountId);
            Assert.Equal(spaceId, unitOfWork.Context.SpaceId);
            Assert.Equal(Role.Member, unitOfWork.Context.Role);

            // The DbContext's application-state layer is the resolved Space, not merely the
            // requested one - the same value, recorded as verified.
            Assert.Equal(unitOfWork.Context, scope.Resolved);
        }
    }

    /// <summary>
    /// AC1 - a Space the Account holds no Membership in is refused, not defaulted - and a Space that
    /// does not exist is refused identically.
    /// </summary>
    /// <remarks>
    /// Anchored on the positive case in the same test: the Account IS a member of Space A, and the
    /// resolver finds that, so the two refusals below are the resolver saying no rather than the
    /// resolver being unable to find anything. Space B exists and has a member - just not this one.
    /// </remarks>
    [Fact]
    public async Task A_Space_the_Account_is_not_a_member_of_is_refused_exactly_as_a_missing_one_is()
    {
        var database = Available();
        var member = SpaceSeeding.NewAccount();
        var other = SpaceSeeding.NewAccount();
        var spaceA = Guid.NewGuid();
        var spaceB = Guid.NewGuid();

        await SpaceSeeding.SeedAsync(database, spaceA, context =>
        {
            context.Accounts.AddRange(member, other);
            context.Spaces.Add(SpaceSeeding.NewSpace(spaceA));
            context.Memberships.Add(SpaceSeeding.NewMembership(spaceA, member.Id, Role.Owner));
        }, TestContext.Current.CancellationToken);

        await SpaceSeeding.SeedAsync(database, spaceB, context =>
        {
            context.Spaces.Add(SpaceSeeding.NewSpace(spaceB));
            context.Memberships.Add(SpaceSeeding.NewMembership(spaceB, other.Id, Role.Owner));
        }, TestContext.Current.CancellationToken);

        Assert.NotNull(await ResolveRoleAsync(database, member.Id, spaceA));

        Assert.Null(await ResolveRoleAsync(database, member.Id, spaceB));
        Assert.Null(await ResolveRoleAsync(database, member.Id, Guid.NewGuid()));
    }

    /// <summary>
    /// A refused resolution leaves the request's scope matching nothing.
    /// </summary>
    /// <remarks>
    /// Fail closed. Narrowing to the requested Space happens before the Membership is verified, so a
    /// refusal that left it narrowed would leave a context whose query filters admit a Space the
    /// caller was just refused from.
    /// </remarks>
    [Fact]
    public async Task A_refused_resolution_leaves_the_scope_matching_no_Space()
    {
        var database = Available();
        var (context, scope) = Request(database);

        await using (context)
        {
            var refused = await new MembershipActiveSpaceResolver(context, scope)
                .BeginAsync(Guid.NewGuid(), Guid.NewGuid(), TestContext.Current.CancellationToken);

            Assert.Null(refused);
            Assert.Null(scope.SpaceId);
            Assert.Null(scope.Resolved);
        }
    }

    /// <summary>
    /// AC7 - the Role comes from the Membership row as it is on THIS request, so a Role changed
    /// between two requests reaches the second with no re-authentication.
    /// </summary>
    /// <remarks>
    /// A cache anywhere between the row and the context - a singleton scope, a memoised read, a Role
    /// kept from the first request - returns <c>Member</c> the second time and fails this.
    /// </remarks>
    [Fact]
    public async Task The_Role_is_read_from_the_Membership_row_on_every_request()
    {
        var database = Available();
        var account = SpaceSeeding.NewAccount();
        var spaceId = Guid.NewGuid();
        var membership = SpaceSeeding.NewMembership(spaceId, account.Id, Role.Member);

        await SpaceSeeding.SeedAsync(database, spaceId, context =>
        {
            context.Accounts.Add(account);
            context.Spaces.Add(SpaceSeeding.NewSpace(spaceId));
            context.Memberships.Add(membership);
        }, TestContext.Current.CancellationToken);

        Assert.Equal(Role.Member, await ResolveRoleAsync(database, account.Id, spaceId));

        await SpaceSeeding.SeedAsync(database, spaceId, context =>
        {
            context.Memberships.Attach(membership);
            membership.Role = Role.Viewer;
        }, TestContext.Current.CancellationToken);

        Assert.Equal(Role.Viewer, await ResolveRoleAsync(database, account.Id, spaceId));
    }

    /// <summary>
    /// AC2 - a resolved unit of work reads its own Space's rows and no other's, through the
    /// product's reader.
    /// </summary>
    [Fact]
    public async Task A_resolved_unit_of_work_reads_only_its_own_Spaces_rows()
    {
        var database = Available();
        var account = SpaceSeeding.NewAccount();
        var (spaceA, _) = await SeedTwoSpacesAsync(database, account);

        var (context, scope) = Request(database);

        await using (context)
        {
            await using var unitOfWork = await new MembershipActiveSpaceResolver(context, scope)
                .BeginAsync(account.Id, spaceA, TestContext.Current.CancellationToken);

            Assert.NotNull(unitOfWork);

            var statuses = await new StatusDefinitionReader(context).ListAsync(TestContext.Current.CancellationToken);

            Assert.Equal(["Alpha"], statuses.Select(status => status.Name));
        }
    }

    /// <summary>
    /// AC5 - the database is configured for serial plans.
    /// </summary>
    /// <remarks>
    /// AD-2 (<c>:83</c>): a parallel plan reading <c>SESSION_CONTEXT()</c> on a pool-reset session
    /// "can return another tenant's rows, silently and successfully". <c>MAXDOP = 1</c> removes the
    /// class. Asserted against a database built from migrations, because that is what the fixture
    /// creates and what a deployment will create - a setting applied by hand to a developer's
    /// instance would pass a check written against that instance and ship nothing. Set by the
    /// <c>ActiveSpaceResolution</c> migration, as a database-scoped configuration.
    /// </remarks>
    [Fact]
    public async Task The_database_is_configured_for_serial_plans()
    {
        var database = Available();

        await using var connection = await database.OpenWithoutSpaceContextAsync(
            TestContext.Current.CancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT CAST(value AS int)
            FROM sys.database_scoped_configurations
            WHERE name = N'MAXDOP'
            """;

        var result = await command.ExecuteScalarAsync(TestContext.Current.CancellationToken);

        Assert.Equal(
            SerialPlanDegreeOfParallelism,
            Convert.ToInt32(result, CultureInfo.InvariantCulture));
    }

    /// <summary>
    /// AC6 - the principal key a composite foreign key carrying SpaceId must target.
    /// </summary>
    /// <remarks>
    /// There is no cross-entity reference inside a Space yet; the first is FR-21's Assignee,
    /// <c>(SpaceId, MembershipId)</c>, in story 4.6. A composite foreign key needs a matching
    /// unique or alternate key on the principal and <c>Membership</c>'s primary key is <c>Id</c>
    /// alone, so the enabling constraint is what this story delivers. Asserting it here is what
    /// makes story 4.6 inherit a fact rather than a convention.
    /// </remarks>
    [Fact]
    public async Task Membership_carries_a_unique_key_on_SpaceId_and_Id()
    {
        var database = Available();

        await using var connection = await database.OpenWithoutSpaceContextAsync(
            TestContext.Current.CancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT COUNT(*)
            FROM sys.indexes i
            JOIN sys.index_columns ic ON ic.object_id = i.object_id AND ic.index_id = i.index_id
            JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
            JOIN sys.tables t ON t.object_id = i.object_id
            WHERE t.name = @table AND i.is_unique = 1 AND c.name IN (@spaceId, N'Id')
            GROUP BY i.index_id
            HAVING COUNT(*) = 2 AND MAX(ic.key_ordinal) = 2
            """;

        command.Parameters.Add(new SqlParameter("@table", SchemaNames.MembershipTable));
        command.Parameters.Add(new SqlParameter("@spaceId", SchemaNames.SpaceIdColumn));

        var result = await command.ExecuteScalarAsync(TestContext.Current.CancellationToken);

        Assert.NotNull(result);
    }

    /// <summary>
    /// AC2 and AC5 - the two SQL Server behaviours the whole session-context design rests on.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Verified by execution, because this project has twice shipped a plausible-but-wrong SQL
    /// Server claim.</b> <c>review-web-verification.md:40-46</c> records both and recommends
    /// treating any assertion about engine behaviour as unverified until checked.
    /// <c>deferred-work.md:185</c> recorded these two specifically as "inferred from documented SQL
    /// Server semantics rather than executed" and asked for "ten minutes against a live server".
    /// This is that ten minutes, kept as a permanent test so the assumption cannot rot.
    /// </para>
    /// <para>
    /// The two claims: a pooled connection returned and re-taken has had its session context
    /// cleared by <c>sp_reset_connection</c>, and a second set of a key already written
    /// <c>@read_only = 1</c> is refused. The first is what makes AC2's "never once per connection"
    /// satisfiable at all; the second is why story 4.3 cannot write the invited Membership on
    /// registration's unit of work.
    /// </para>
    /// </remarks>
    [Fact]
    public async Task A_read_only_session_key_is_cleared_by_the_pool_and_refuses_a_second_set()
    {
        var database = Available();

        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        short firstSession;

        // Claim 2 first, on one connection: the key will not move once it is read-only.
        await using (var pinned = await database.OpenForSpaceAsync(
            first, TestContext.Current.CancellationToken))
        {
            firstSession = await SessionIdOfAsync(pinned, TestContext.Current.CancellationToken);

            await using var reset = pinned.CreateCommand();
            reset.CommandText = "EXEC sp_set_session_context @key = @key, @value = @value, @read_only = 1";
            reset.Parameters.Add(new SqlParameter("@key", SqlDbType.NVarChar, 128) { Value = SchemaNames.SpaceIdSessionKey });
            reset.Parameters.Add(new SqlParameter("@value", SqlDbType.Variant) { Value = second });

            var refused = await Assert.ThrowsAsync<SqlException>(
                () => reset.ExecuteNonQueryAsync(TestContext.Current.CancellationToken));

            Assert.Equal(ReadOnlySessionKeyErrorNumber, refused.Number);
        }

        // Claim 1: the connection above has gone back to the pool. A new one must not inherit its
        // context - if it does, AC2's "never once per connection" is unachievable and the pooled
        // connection case is a real leak rather than a theoretical one.
        await using var reused = await database.OpenWithoutSpaceContextAsync(
            TestContext.Current.CancellationToken);

        // Without this the claim is vacuous: a pool that handed out a FRESH connection would carry
        // no context either, and the assertion below would pass having observed no reuse at all.
        Assert.Equal(firstSession, await SessionIdOfAsync(reused, TestContext.Current.CancellationToken));

        await using var read = reused.CreateCommand();
        read.CommandText = "SELECT SESSION_CONTEXT(N'SpaceId')";

        var carried = await read.ExecuteScalarAsync(TestContext.Current.CancellationToken);

        Assert.True(carried is null or DBNull, "A pooled connection carried a session context into its next use.");
    }

    /// <summary>
    /// AC8 - the chosen B2 seeding strategy actually populates two Spaces, each readable only under
    /// its own context.
    /// </summary>
    /// <remarks>
    /// Blocker B2 (<c>test-design-architecture.md:109</c>) is owned by this story and consumed by
    /// story 1.9: every isolation test needs two Spaces populated, and the policy makes that an
    /// architectural choice rather than a fixture detail. Proving the strategy by executing it is
    /// worth more than stating it in prose. Read back over raw connections rather than through EF,
    /// so what is asserted is what the database holds and the policy admits, with no query filter
    /// in the way.
    /// </remarks>
    [Fact]
    public async Task Two_Spaces_can_be_seeded_and_each_is_readable_only_under_its_own_context()
    {
        var database = Available();
        var (spaceA, spaceB) = await SeedTwoSpacesAsync(database);

        await using (var underA = await database.OpenForSpaceAsync(
            spaceA, TestContext.Current.CancellationToken))
        {
            Assert.Equal(["Alpha"], await StatusNamesAsync(underA, TestContext.Current.CancellationToken));
        }

        await using var underB = await database.OpenForSpaceAsync(
            spaceB, TestContext.Current.CancellationToken);

        Assert.Equal(["Beta"], await StatusNamesAsync(underB, TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// AC8 - the seeding shape satisfies the BLOCK predicate rather than evading it: a block cannot
    /// write into a Space other than the one it was opened for.
    /// </summary>
    [Fact]
    public async Task A_seeding_block_cannot_write_into_another_Space()
    {
        var database = Available();
        var spaceA = Guid.NewGuid();
        var spaceB = Guid.NewGuid();

        await SpaceSeeding.SeedAsync(database, spaceB, context =>
            context.Spaces.Add(SpaceSeeding.NewSpace(spaceB)), TestContext.Current.CancellationToken);

        await Assert.ThrowsAsync<DbUpdateException>(() => SpaceSeeding.SeedAsync(
            database,
            spaceA,
            context => context.StatusDefinitions.Add(SpaceSeeding.NewStatus(spaceB, "Intruder")),
            TestContext.Current.CancellationToken));

        await using var underB = await database.OpenForSpaceAsync(spaceB, TestContext.Current.CancellationToken);

        Assert.Empty(await StatusNamesAsync(underB, TestContext.Current.CancellationToken));
    }

    private static (YelloDbContext Context, SpaceScope Scope) Request(RegistrationDatabase database)
    {
        var scope = new SpaceScope();

        return (database.CreateContext(scope), scope);
    }

    private static async Task<Role?> ResolveRoleAsync(RegistrationDatabase database, Guid accountId, Guid spaceId)
    {
        var (context, scope) = Request(database);

        await using (context)
        {
            await using var unitOfWork = await new MembershipActiveSpaceResolver(context, scope)
                .BeginAsync(accountId, spaceId, TestContext.Current.CancellationToken);

            return unitOfWork?.Context.Role;
        }
    }

    /// <summary>
    /// Space A with a Status named Alpha, Space B with one named Beta - and, when an Account is
    /// given, that Account a Member of both.
    /// </summary>
    private static async Task<(Guid SpaceA, Guid SpaceB)> SeedTwoSpacesAsync(
        RegistrationDatabase database,
        Account? member = null)
    {
        var spaceA = Guid.NewGuid();
        var spaceB = Guid.NewGuid();

        await SpaceSeeding.SeedAsync(database, spaceA, context =>
        {
            context.Spaces.Add(SpaceSeeding.NewSpace(spaceA));
            context.StatusDefinitions.Add(SpaceSeeding.NewStatus(spaceA, "Alpha"));

            if (member is not null)
            {
                context.Accounts.Add(member);
                context.Memberships.Add(SpaceSeeding.NewMembership(spaceA, member.Id, Role.Member));
            }
        }, TestContext.Current.CancellationToken);

        await SpaceSeeding.SeedAsync(database, spaceB, context =>
        {
            context.Spaces.Add(SpaceSeeding.NewSpace(spaceB));
            context.StatusDefinitions.Add(SpaceSeeding.NewStatus(spaceB, "Beta"));

            if (member is not null)
            {
                context.Memberships.Add(SpaceSeeding.NewMembership(spaceB, member.Id, Role.Member));
            }
        }, TestContext.Current.CancellationToken);

        return (spaceA, spaceB);
    }

    private static async Task<List<string>> StatusNamesAsync(
        SqlConnection connection,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT Name FROM dbo.[StatusDefinition] ORDER BY Name";

        var names = new List<string>();

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            names.Add(reader.GetString(0));
        }

        return names;
    }

    private static async Task<short> SessionIdOfAsync(
        SqlConnection connection,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT @@SPID";

        return Convert.ToInt16(await command.ExecuteScalarAsync(cancellationToken), CultureInfo.InvariantCulture);
    }

    private RegistrationDatabase Available()
    {
        Assert.SkipUnless(fixture.IsAvailable, fixture.UnavailableReason ?? string.Empty);

        return fixture.Database!;
    }
}
