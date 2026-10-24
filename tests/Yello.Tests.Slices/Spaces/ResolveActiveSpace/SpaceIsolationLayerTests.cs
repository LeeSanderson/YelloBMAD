using System.Data;
using System.Globalization;
using System.Reflection;
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

    // ---------------------------------------------------------------------------------------------
    // Story 1.7 - the Account-scoped context (AD-24 / AR-29): a second session-context key,
    // `AccountId`, that widens exactly one table's filter - `Membership` - in both layers. Written by
    // the ATDD run on 2026-10-24. Three are red-phase scaffolds; one is green today and is left
    // active as a standing gate (see its remarks).
    //
    // The key is set here by a raw `sp_set_session_context`, in TEST code, because the product's
    // entry point for it does not exist yet. `SpaceResolutionSeamTests` counts production call sites
    // under Yello.Infrastructure only, so these do not count against it. On activation, prefer the
    // product's Account-scoped entry point where one is reachable.
    // ---------------------------------------------------------------------------------------------

    /// <summary>
    /// AC3 / Q1 - under the <c>AccountId</c> key and nothing else, row-level security alone admits
    /// exactly that Account's Memberships, in every Space it holds one in, and no other Account's -
    /// including another Account's Membership of a Space they share.
    /// </summary>
    /// <remarks>
    /// Raw SQL on a raw connection: no query filter exists on this path, so the policy is the only
    /// thing deciding. Two Accounts with different ids, sharing a Space, so a predicate that admitted
    /// by Space rather than by Account would show the other Account's row.
    /// </remarks>
    [Fact(Skip = "red: AC3 - Membership's filter predicate does not read SESSION_CONTEXT('AccountId') yet (story 1.7 Task 2, open question 1)")]
    [Trait("Requirement", "AR-29")]
    [Trait("Requirement", "AD-24")]
    public async Task Under_the_AccountId_key_row_level_security_alone_admits_exactly_that_Accounts_Memberships()
    {
        var database = Available();
        var (a, b, shared, onlyA, onlyB) = await SeedTwoAccountsSharingASpaceAsync(database);

        await using (var asA = await OpenForAccountAsync(database, a.Id))
        {
            var visible = await MembershipsAsync(asA);

            Assert.Equal(new[] { shared, onlyA }.Order(), visible.Select(row => row.SpaceId).Order());
            Assert.All(visible, row => Assert.Equal(a.Id, row.AccountId));
        }

        await using var asB = await OpenForAccountAsync(database, b.Id);

        var visibleToB = await MembershipsAsync(asB);

        Assert.Equal(new[] { shared, onlyB }.Order(), visibleToB.Select(row => row.SpaceId).Order());
        Assert.All(visibleToB, row => Assert.Equal(b.Id, row.AccountId));
    }

    /// <summary>
    /// AC3 - the <c>AccountId</c> key widens <c>Membership</c> and nothing else: under it, no
    /// <c>StatusDefinition</c> and no <c>SpacePresence</c> row of any of the Account's Spaces is
    /// visible.
    /// </summary>
    /// <remarks>
    /// Anchored on the widening itself: the same connection does see the Account's Memberships, so
    /// the two empty reads are the other policies holding rather than a connection that sees nothing.
    /// </remarks>
    [Fact(Skip = "red: AC3 - Membership's filter predicate does not read SESSION_CONTEXT('AccountId') yet, so the anchor read is empty (story 1.7 Task 2)")]
    [Trait("Requirement", "AR-29")]
    [Trait("Requirement", "AD-24")]
    public async Task Under_the_AccountId_key_no_other_Space_scoped_table_is_visible()
    {
        var database = Available();
        var (a, _, shared, onlyA, _) = await SeedTwoAccountsSharingASpaceAsync(database);

        foreach (var spaceId in new[] { shared, onlyA })
        {
            await using var underSpace = await database.OpenForSpaceAsync(spaceId, TestContext.Current.CancellationToken);
            await using var presence = underSpace.CreateCommand();

            presence.CommandText = "INSERT INTO dbo.[SpacePresence] (SpaceId, AccountId, ExpiresAt) VALUES (@space, @account, @expires)";
            presence.Parameters.Add(new SqlParameter("@space", spaceId));
            presence.Parameters.Add(new SqlParameter("@account", a.Id));
            presence.Parameters.Add(new SqlParameter("@expires", DateTimeOffset.UtcNow.AddMinutes(10)));

            await presence.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
        }

        await using var asA = await OpenForAccountAsync(database, a.Id);

        Assert.Equal(2, (await MembershipsAsync(asA)).Count);

        Assert.Equal(0, await RegistrationDatabase.CountAsync(asA, SchemaNames.StatusDefinitionTable, TestContext.Current.CancellationToken));
        Assert.Equal(0, await RegistrationDatabase.CountAsync(asA, SchemaNames.SpacePresenceTable, TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// AC3 / AD-2 - the EF query filter alone, from application state, admits only the scoped
    /// Account's Memberships, even where row-level security admits another Account's.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The two layers are set to disagree, as <see cref="The_query_filter_alone_excludes_another_Spaces_rows"/>
    /// does for Spaces: the session context names the shared Space, so the policy admits both
    /// Accounts' Memberships of it, while the application state names Account A and no Space. Only
    /// the filter can exclude B's row. The same connection with the filter disarmed is read first.
    /// </para>
    /// <para>
    /// <b>Reached by name.</b> <c>AccountScope</c> and the three-argument <see cref="YelloDbContext"/>
    /// constructor are story 1.7 Task 2's; on activation replace both lookups with typed references.
    /// </para>
    /// </remarks>
    [Fact(Skip = "red: AC3 - AccountScope and the Membership filter's AccountId disjunct do not exist yet (story 1.7 Task 2)")]
    [Trait("Requirement", "AR-7")]
    [Trait("Requirement", "AR-29")]
    [Trait("Requirement", "AD-24")]
    public async Task The_query_filter_alone_admits_only_the_scoped_Accounts_Memberships()
    {
        var database = Available();
        var (a, b, shared, _, _) = await SeedTwoAccountsSharingASpaceAsync(database);

        await using var context = ContextScopedToAccount(a.Id);
        await using var transaction = await context.Database.BeginTransactionAsync(TestContext.Current.CancellationToken);

        await SpaceSessionContext.SetAsync(context.Database, shared, TestContext.Current.CancellationToken);

        var admittedByRowLevelSecurity = await context.Memberships
            .IgnoreQueryFilters()
            .Select(membership => membership.AccountId)
            .ToListAsync(TestContext.Current.CancellationToken);

        Assert.Equal(new[] { a.Id, b.Id }.Order(), admittedByRowLevelSecurity.Order());

        var visible = await context.Memberships
            .Select(membership => new { membership.AccountId, membership.SpaceId })
            .ToListAsync(TestContext.Current.CancellationToken);

        var only = Assert.Single(visible);
        Assert.Equal(a.Id, only.AccountId);
        Assert.Equal(shared, only.SpaceId);
    }

    /// <summary>
    /// AC3 / Task 2 "one key per unit of work" - a Space-scoped unit of work, for an Account that
    /// holds Memberships in several Spaces, sees only its Membership of the requested Space, through
    /// either layer alone.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Green today, and left active as a standing gate.</b> Today both layers are keyed on
    /// <c>SpaceId</c> alone, so this holds trivially. It is the regression gate for story 1.7's
    /// widening: if a Space-scoped unit of work ever also set the <c>AccountId</c> key, or narrowed
    /// <c>AccountScope</c>, the widened predicate and filter would admit the Account's Memberships
    /// in every other Space - and this fails, in whichever layer leaked.
    /// </para>
    /// <para>
    /// <b>Not plantable before the widening, and that is recorded rather than papered over.</b> While
    /// both layers are keyed on <c>SpaceId</c> alone, no leak of this shape can be constructed: a filter
    /// planted to admit the Account everywhere is masked by the policy, and the policy cannot be
    /// planted without a migration. Story 1.7 plants it once the widening exists - "a Space-scoped
    /// unit of work also sets <c>AccountId</c>" (RLS half) and "<c>AccountScope</c> narrowed in a
    /// Space-scoped unit of work" (filter half) - and records both in its plant table.
    /// </para>
    /// </remarks>
    [Fact]
    [Trait("Requirement", "AR-29")]
    [Trait("Requirement", "AD-24")]
    public async Task A_Space_scoped_unit_of_work_sees_only_its_own_Membership_of_an_Account_in_several_Spaces()
    {
        var database = Available();
        var (a, _, shared, onlyA, _) = await SeedTwoAccountsSharingASpaceAsync(database);

        var (context, scope) = Request(database);

        await using (context)
        {
            await using var unitOfWork = await new MembershipActiveSpaceResolver(context, scope)
                .BeginAsync(a.Id, shared, TestContext.Current.CancellationToken);

            Assert.NotNull(unitOfWork);

            var throughBothLayers = await context.Memberships
                .Where(membership => membership.AccountId == a.Id)
                .Select(membership => membership.SpaceId)
                .ToListAsync(TestContext.Current.CancellationToken);

            var throughRowLevelSecurityAlone = await context.Memberships
                .IgnoreQueryFilters()
                .Where(membership => membership.AccountId == a.Id)
                .Select(membership => membership.SpaceId)
                .ToListAsync(TestContext.Current.CancellationToken);

            Assert.Equal([shared], throughBothLayers);
            Assert.Equal([shared], throughRowLevelSecurityAlone);
            Assert.DoesNotContain(onlyA, throughBothLayers);
        }
    }

    /// <summary>
    /// Account A: Member of a shared Space and Owner of one of its own. Account B: Member of the
    /// shared Space and Owner of one of its own. Every Membership's Account is added before it
    /// (<c>FK_Membership_Account_AccountId</c>), every Space in the block that first writes into it
    /// (<c>FK_Membership_Space_SpaceId</c>), one Owner per Space (<c>UX_Membership_SpaceId_Owner</c>),
    /// one Membership per Account per Space (<c>IX_Membership_SpaceId_AccountId</c>), and a Status in
    /// each Space so a widening that leaked beyond <c>Membership</c> has a row to show.
    /// </summary>
    private static async Task<(Account A, Account B, Guid Shared, Guid OnlyA, Guid OnlyB)> SeedTwoAccountsSharingASpaceAsync(
        RegistrationDatabase database)
    {
        var a = SpaceSeeding.NewAccount();
        var b = SpaceSeeding.NewAccount();
        var shared = Guid.NewGuid();
        var onlyA = Guid.NewGuid();
        var onlyB = Guid.NewGuid();

        await SpaceSeeding.SeedAsync(database, shared, context =>
        {
            context.Accounts.AddRange(a, b);
            context.Spaces.Add(SpaceSeeding.NewSpace(shared));
            context.StatusDefinitions.Add(SpaceSeeding.NewStatus(shared, "Shared"));
            context.Memberships.Add(SpaceSeeding.NewMembership(shared, a.Id, Role.Member));
            context.Memberships.Add(SpaceSeeding.NewMembership(shared, b.Id, Role.Member));
        }, TestContext.Current.CancellationToken);

        foreach (var (spaceId, owner) in new[] { (onlyA, a), (onlyB, b) })
        {
            await SpaceSeeding.SeedAsync(database, spaceId, context =>
            {
                context.Spaces.Add(SpaceSeeding.NewSpace(spaceId));
                context.StatusDefinitions.Add(SpaceSeeding.NewStatus(spaceId, "Own"));
                context.Memberships.Add(SpaceSeeding.NewMembership(spaceId, owner.Id, Role.Owner));
            }, TestContext.Current.CancellationToken);
        }

        return (a, b, shared, onlyA, onlyB);
    }

    /// <summary>
    /// A connection with the <c>AccountId</c> key set <c>@read_only = 1</c> and no <c>SpaceId</c> key -
    /// the session context an Account-scoped unit of work runs under (AD-24).
    /// </summary>
    internal static async Task<SqlConnection> OpenForAccountAsync(RegistrationDatabase database, Guid accountId)
    {
        var connection = await database.OpenWithoutSpaceContextAsync(TestContext.Current.CancellationToken);

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "EXEC sp_set_session_context @key = @key, @value = @value, @read_only = 1";
            command.Parameters.Add(new SqlParameter("@key", SqlDbType.NVarChar, 128) { Value = "AccountId" });
            command.Parameters.Add(new SqlParameter("@value", SqlDbType.Variant) { Value = accountId });

            await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);

            return connection;
        }
        catch
        {
            await connection.DisposeAsync();
            throw;
        }
    }

    /// <summary>
    /// Every <c>Membership</c> row the connection's session context admits, read raw.
    /// </summary>
    internal static async Task<List<(Guid SpaceId, Guid AccountId)>> MembershipsAsync(SqlConnection connection)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT SpaceId, AccountId FROM dbo.[Membership]";

        var rows = new List<(Guid SpaceId, Guid AccountId)>();

        await using var reader = await command.ExecuteReaderAsync(TestContext.Current.CancellationToken);

        while (await reader.ReadAsync(TestContext.Current.CancellationToken))
        {
            rows.Add((reader.GetGuid(0), reader.GetGuid(1)));
        }

        return rows;
    }

    /// <summary>
    /// A request-shaped context whose application state names an Account and no Space. Reached by
    /// name because <c>AccountScope</c> is story 1.7's; the failure message says which part is
    /// missing.
    /// </summary>
    private YelloDbContext ContextScopedToAccount(Guid accountId)
    {
        const string accountScopeName = "Yello.Infrastructure.Persistence.AccountScope";

        var accountScopeType = typeof(SpaceScope).Assembly.GetType(accountScopeName, throwOnError: false);

        Assert.True(accountScopeType is not null, $"{accountScopeName} is not in Yello.Infrastructure - story 1.7 Task 2 writes it.");

        var accountScope = Activator.CreateInstance(accountScopeType)!;

        var narrow = accountScopeType.GetMethod("Narrow", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        Assert.True(narrow is not null, $"{accountScopeName} has no Narrow(Guid) - story 1.7 Task 2 mirrors SpaceScope.");

        narrow.Invoke(accountScope, [accountId]);

        var constructor = Array.Find(
            typeof(YelloDbContext).GetConstructors(),
            candidate => Array.Exists(candidate.GetParameters(), parameter => parameter.ParameterType == accountScopeType));

        Assert.True(constructor is not null, "YelloDbContext takes no AccountScope - story 1.7 Task 2 adds it to the constructor.");

        var options = new DbContextOptionsBuilder<YelloDbContext>().UseSqlServer(fixture.ConnectionString!).Options;

        object? ArgumentFor(ParameterInfo parameter)
        {
            if (parameter.ParameterType == accountScopeType)
            {
                return accountScope;
            }

            return parameter.ParameterType == typeof(DbContextOptions<YelloDbContext>) ? options : null;
        }

        var arguments = constructor.GetParameters().Select(ArgumentFor).ToArray();

        return Assert.IsType<YelloDbContext>(constructor.Invoke(arguments));
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
