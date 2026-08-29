using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Xunit;
using Yello.Application.Accounts.RegisterAccount;
using Yello.Domain.Accounts;
using Yello.Domain.Memberships;
using Yello.Domain.Spaces;
using Yello.Infrastructure.Identity;
using Yello.Infrastructure.Persistence;

namespace Yello.Tests.Slices.Accounts.RegisterAccount;

/// <summary>
/// Registration against a real, migrated SQL Server. AC1, AC2, AC3, AC5.
/// </summary>
/// <remarks>
/// Every assertion here needs the database to be real: one transaction, a filtered unique index,
/// a row-level security policy and a stored hash are all engine behaviour. The story's standing
/// caveat applies throughout - two plausible-but-wrong SQL Server claims have already been caught
/// in this project, "both in <i>index behaviour</i> specifically" - so nothing below is asserted
/// from documentation alone.
/// </remarks>
[Trait("Suite", "Slices")]
[Trait("Priority", "P0")]
[Trait("Requirement", "AR-27")]
[Trait("Requirement", "AD-22")]
[Trait("Requirement", "AR-28")]
[Trait("Requirement", "AD-23")]
[Trait("Requirement", "AR-12")]
[Trait("Requirement", "AD-5")]
[Trait("Requirement", "NFR-1")]
[Trait("Requirement", "NFR-6")]
public sealed class RegisterAccountIntegrationTests(MigratedDatabaseFixture fixture)
    : IClassFixture<MigratedDatabaseFixture>
{
    private const int ExpectedDefaultStatusCount = 3;
    private const string Password = "a-password-nobody-else-uses-1!";

    [Fact]
    public async Task Registration_commits_an_Account_a_Space_an_Owner_Membership_and_its_Statuses()
    {
        var database = Available();
        var address = RandomAddress();

        var spaceName = await RegisterAsync(database, address);

        await using var context = database.CreateContext();

        var account = await context.Accounts.SingleAsync(
            candidate => candidate.NormalizedEmailAddress == EmailAddressNormalisation.Normalise(address),
            TestContext.Current.CancellationToken);

        var space = await context.Spaces.SingleAsync(
            candidate => candidate.Name == spaceName, TestContext.Current.CancellationToken);

        // Space-scoped rows need the session context to be visible at all - which is the point of
        // AD-2, and is why this reads through a connection scoped to the Space rather than
        // through the context above.
        await using var scoped = await database.OpenForSpaceAsync(
            space.Id, TestContext.Current.CancellationToken);

        Assert.Equal(1, await RegistrationDatabase.CountAsync(
            scoped, SchemaNames.MembershipTable, TestContext.Current.CancellationToken));

        Assert.Equal(ExpectedDefaultStatusCount, await RegistrationDatabase.CountAsync(
            scoped, SchemaNames.StatusDefinitionTable, TestContext.Current.CancellationToken));

        var role = await ScalarAsync(
            scoped,
            $"SELECT TOP 1 [Role] FROM dbo.[{SchemaNames.MembershipTable}]",
            TestContext.Current.CancellationToken);

        Assert.Equal(nameof(Role.Owner), role);
        Assert.Equal(address, account.EmailAddress);
    }

    /// <summary>
    /// AC2: no second Account and no second Space are created.
    /// </summary>
    [Fact]
    public async Task A_second_registration_for_the_same_address_creates_nothing()
    {
        var database = Available();
        var address = RandomAddress();

        _ = await RegisterAsync(database, address);

        await using var context = database.CreateContext();
        var spacesAfterFirst = await context.Spaces.CountAsync(TestContext.Current.CancellationToken);

        await RegisterAsync(database, address);

        var accounts = await context.Accounts.CountAsync(
            candidate => candidate.NormalizedEmailAddress == EmailAddressNormalisation.Normalise(address),
            TestContext.Current.CancellationToken);

        var spacesAfterSecond = await context.Spaces.CountAsync(TestContext.Current.CancellationToken);

        Assert.Equal(1, accounts);
        Assert.Equal(spacesAfterFirst, spacesAfterSecond);
    }

    /// <summary>
    /// AC5: "the password appears in none of them" - the datastore half.
    /// </summary>
    /// <remarks>
    /// Reads every character column of every user table from the catalogue rather than checking
    /// the one column a password is expected in. Checking <c>PasswordHash</c> alone would prove
    /// almost nothing: the failure worth catching is a password copied somewhere nobody thought
    /// to look, and a catalogue-driven scan covers tables later stories add without anyone
    /// extending this test.
    /// </remarks>
    [Fact]
    public async Task The_password_appears_nowhere_in_the_datastore()
    {
        var database = Available();
        var address = RandomAddress();

        var spaceName = await RegisterAsync(database, address);

        await using var context = database.CreateContext();
        var space = await context.Spaces.SingleAsync(
            candidate => candidate.Name == spaceName, TestContext.Current.CancellationToken);

        await using var scoped = await database.OpenForSpaceAsync(
            space.Id, TestContext.Current.CancellationToken);

        var stored = await RegistrationDatabase.ReadEveryTextValueAsync(
            scoped, TestContext.Current.CancellationToken);

        // The scan has to have actually read something, or an absence assertion over an empty
        // list passes for the wrong reason - which is this suite's defining defect class.
        Assert.Contains(address, stored, StringComparer.Ordinal);

        Assert.DoesNotContain(
            stored,
            value => value.Contains(Password, StringComparison.Ordinal));
    }

    /// <summary>
    /// NFR-6: the work factor is tunable without re-registering existing Accounts.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Asserted as the <i>mechanism</i> rather than as a claim. The iteration count is embedded in
    /// every stored hash, so <c>VerifyHashedPassword</c> can compare it against the configured one
    /// and answer <c>SuccessRehashNeeded</c> - which is what makes raising the number cost nothing
    /// to Accounts that already exist. Story 1.4 performs the rehash on sign-in; this story's
    /// obligation is to store hashes in a form that admits it, and this is that proof.
    /// </para>
    /// <para>
    /// It reads a real stored hash from the database rather than one made up in the test, so it
    /// also proves the column round-trips the encoded form intact.
    /// </para>
    /// </remarks>
    [Fact]
    public async Task Raising_the_work_factor_leaves_an_existing_hash_valid_and_asks_for_a_rehash()
    {
        var database = Available();
        var address = RandomAddress();

        _ = await RegisterAsync(database, address);

        await using var context = database.CreateContext();

        var account = await context.Accounts.SingleAsync(
            candidate => candidate.NormalizedEmailAddress == EmailAddressNormalisation.Normalise(address),
            TestContext.Current.CancellationToken);

        Assert.NotNull(account.PasswordHash);

        // RegisterAsync hashes at the framework default - see its remarks - so THAT is the factor
        // this hash was written with, and the one it should verify cleanly against.
        var written = PasswordWorkFactor.FrameworkDefaultIterationCount;

        var atSameFactor = Hasher(written)
            .VerifyHashedPassword(HashedCredential.Unread, account.PasswordHash, Password);

        Assert.Equal(PasswordVerificationResult.Success, atSameFactor);

        // Raised, the same stored hash still verifies - and reports that it should be rewritten.
        // Nothing has to be re-registered, which is NFR-6 in one assertion. Yello's own chosen
        // factor is above the default, so this is not a hypothetical comparison: it is exactly
        // what an Account registered before a work-factor rise would meet at its next sign-in.
        Assert.True(PasswordWorkFactor.IterationCount > written);

        var atRaisedFactor = Hasher(PasswordWorkFactor.IterationCount)
            .VerifyHashedPassword(HashedCredential.Unread, account.PasswordHash, Password);

        Assert.Equal(PasswordVerificationResult.SuccessRehashNeeded, atRaisedFactor);
    }

    /// <summary>
    /// AD-2 / NFR-1: a Space-scoped row is invisible without the session context that places it.
    /// </summary>
    /// <remarks>
    /// <b>The no-context case is the one that matters</b>, and it is the one a test that always
    /// sets a context can never observe. With no context the predicate compares a
    /// <c>uniqueidentifier</c> against <c>NULL</c>, which is never true, so every Space-scoped row
    /// should be filtered away. This also settles empirically a SQL Server claim the story warns
    /// not to take on trust: the connection here is <c>sa</c>, and row-level security applies to
    /// it exactly as to anyone else.
    /// </remarks>
    [Fact]
    public async Task Space_scoped_rows_are_invisible_without_a_session_context()
    {
        var database = Available();

        var spaceName = await RegisterAsync(database, RandomAddress());

        await using var context = database.CreateContext();
        var space = await context.Spaces.SingleAsync(
            candidate => candidate.Name == spaceName, TestContext.Current.CancellationToken);

        await using (var scoped = await database.OpenForSpaceAsync(
            space.Id, TestContext.Current.CancellationToken))
        {
            Assert.Equal(1, await RegistrationDatabase.CountAsync(
                scoped, SchemaNames.MembershipTable, TestContext.Current.CancellationToken));
        }

        await using (var unscoped = await database.OpenWithoutSpaceContextAsync(
            TestContext.Current.CancellationToken))
        {
            Assert.Equal(0, await RegistrationDatabase.CountAsync(
                unscoped, SchemaNames.MembershipTable, TestContext.Current.CancellationToken));

            Assert.Equal(0, await RegistrationDatabase.CountAsync(
                unscoped, SchemaNames.StatusDefinitionTable, TestContext.Current.CancellationToken));
        }

        // A DIFFERENT Space's context sees nothing either, which is the half that distinguishes
        // "scoped" from "hidden until someone sets any context at all".
        await using var other = await database.OpenForSpaceAsync(
            Guid.NewGuid(), TestContext.Current.CancellationToken);

        Assert.Equal(0, await RegistrationDatabase.CountAsync(
            other, SchemaNames.MembershipTable, TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// AD-5 / AR-12: no Space ever holds zero or two Owner Memberships.
    /// </summary>
    /// <remarks>
    /// The filtered unique index is what holds this, and it is asserted by trying to break it.
    /// A second Membership at any other Role is fine and is inserted first, so the failure below
    /// is attributable to the Role rather than to the Space having any second member at all.
    /// </remarks>
    [Fact]
    public async Task A_Space_cannot_acquire_a_second_Owner_Membership()
    {
        var database = Available();

        var spaceName = await RegisterAsync(database, RandomAddress());

        await using var context = database.CreateContext();
        var space = await context.Spaces.SingleAsync(
            candidate => candidate.Name == spaceName, TestContext.Current.CancellationToken);

        await using var scoped = await database.OpenForSpaceAsync(
            space.Id, TestContext.Current.CancellationToken);

        // A second member at a different Role: permitted, and inserted to prove the index is
        // about the Role rather than about the Space having one Membership.
        await InsertMembershipAsync(
            scoped, space.Id, Guid.NewGuid(), Role.Admin, TestContext.Current.CancellationToken);

        var second = await Assert.ThrowsAsync<SqlException>(() => InsertMembershipAsync(
            scoped, space.Id, Guid.NewGuid(), Role.Owner, TestContext.Current.CancellationToken));

        Assert.Contains(SchemaNames.MembershipOwnerUniqueIndex, second.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// AC3: a failure is a failed transaction, not a repairable state.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The failure is forced at the <i>last</i> row rather than the first: a naming port that
    /// returns a Space name longer than the column, so the Account and Space inserts are already
    /// on the wire when the write fails. That is the shape that would leave "an Account holding
    /// zero Spaces" if the transaction were not real - and an assertion that fails at the first
    /// row would prove nothing, because nothing had been written yet.
    /// </para>
    /// <para>
    /// Note what is asserted: no Account at all. Not "an Account with no Space", which is the
    /// state AD-22 says cannot exist.
    /// </para>
    /// </remarks>
    [Fact]
    public async Task A_registration_that_cannot_complete_leaves_no_Account_behind()
    {
        var database = Available();
        var address = RandomAddress();

        await using var context = database.CreateContext();
        var before = await context.Accounts.CountAsync(TestContext.Current.CancellationToken);

        var handler = new RegisterAccountHandler(
            new IdentityPasswordHasher(Hasher(PasswordWorkFactor.FrameworkDefaultIterationCount)),
            new AccountRegistrationStore(database.CreateContext()),
            new OverlongNaming(),
            new SequentialGuidIdentifierGenerator(),
            TimeProvider.System);

        await Assert.ThrowsAnyAsync<DbUpdateException>(() => handler.HandleAsync(
            new RegisterAccountCommand("Ravi", address, Password),
            TestContext.Current.CancellationToken));

        var after = await context.Accounts.CountAsync(TestContext.Current.CancellationToken);

        Assert.Equal(before, after);

        Assert.False(await context.Accounts.AnyAsync(
            candidate => candidate.NormalizedEmailAddress == EmailAddressNormalisation.Normalise(address),
            TestContext.Current.CancellationToken));
    }

    private RegistrationDatabase Available()
    {
        Assert.SkipUnless(fixture.IsAvailable, fixture.UnavailableReason ?? string.Empty);

        return fixture.Database!;
    }

    private static string RandomAddress() => $"person-{Guid.NewGuid():N}@example.test";

    private static PasswordHasher<HashedCredential> Hasher(int iterationCount) =>
        new(Options.Create(new PasswordHasherOptions
        {
            CompatibilityMode = PasswordHasherCompatibilityMode.IdentityV3,
            IterationCount = iterationCount,
        }));

    /// <summary>
    /// Registers through the real slice, the real store and the real hasher.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The framework's default work factor rather than Yello's, deliberately: these tests assert
    /// behaviour rather than duration, and 220,000 iterations would add roughly a quarter of a
    /// second to every registration in the class for no assertion's benefit. The duration
    /// comparison that DOES depend on the real figure uses it.
    /// </para>
    /// <para>
    /// <b>Returns the Space's name, which is how a test finds the rows it just wrote.</b> One
    /// database serves the whole class, so <c>Spaces.Single()</c> sees every other test's Space
    /// too - which is how these first failed. The obvious fix, having the handler return the
    /// Space it created, is exactly the one that must not be taken: the handler returns nothing
    /// so that no caller can tell a new registration from a duplicate. A unique display name per
    /// registration gives a unique Space name, which identifies the rows without the production
    /// code telling anyone anything.
    /// </para>
    /// </remarks>
    private static async Task<string> RegisterAsync(RegistrationDatabase database, string address)
    {
        await using var context = database.CreateContext();

        var naming = new SuffixNaming();
        var displayName = "Ravi-" + Guid.NewGuid().ToString("N");

        var handler = new RegisterAccountHandler(
            new IdentityPasswordHasher(Hasher(PasswordWorkFactor.FrameworkDefaultIterationCount)),
            new AccountRegistrationStore(context),
            naming,
            new SequentialGuidIdentifierGenerator(),
            TimeProvider.System);

        await handler.HandleAsync(
            new RegisterAccountCommand(displayName, address, Password),
            TestContext.Current.CancellationToken);

        return naming.NameFor(displayName);
    }

    private static async Task InsertMembershipAsync(
        SqlConnection connection,
        Guid spaceId,
        Guid accountId,
        Role role,
        CancellationToken cancellationToken)
    {
        // The Account foreign key has to be satisfied first, or the insert fails for a reason
        // that has nothing to do with the index under test.
        await using (var account = connection.CreateCommand())
        {
            // CA2100 disabled for both statements below: the only interpolated values are this
            // suite's own SchemaNames constants, and every caller-supplied value is a parameter.
#pragma warning disable CA2100 // Review SQL queries for security vulnerabilities
            account.CommandText = $"""
                INSERT INTO dbo.[{SchemaNames.AccountTable}]
                    (Id, EmailAddress, NormalizedEmailAddress, DisplayName, PasswordHash, CreatedAt)
                VALUES (@id, @address, @normalized, @name, NULL, SYSDATETIMEOFFSET())
                """;

            var address = $"member-{accountId:N}@example.test";
            account.Parameters.Add(new SqlParameter("@id", accountId));
            account.Parameters.Add(new SqlParameter("@address", address));
            account.Parameters.Add(new SqlParameter("@normalized", address.ToUpperInvariant()));
            account.Parameters.Add(new SqlParameter("@name", "Member"));

            await account.ExecuteNonQueryAsync(cancellationToken);
        }

        await using var command = connection.CreateCommand();
        command.CommandText = $"""
            INSERT INTO dbo.[{SchemaNames.MembershipTable}] (Id, SpaceId, AccountId, [Role], CreatedAt)
            VALUES (NEWID(), @spaceId, @accountId, @role, SYSDATETIMEOFFSET())
            """;

#pragma warning restore CA2100

        command.Parameters.Add(new SqlParameter("@spaceId", spaceId));
        command.Parameters.Add(new SqlParameter("@accountId", accountId));
        command.Parameters.Add(new SqlParameter("@role", role.ToString()));

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<string?> ScalarAsync(
        SqlConnection connection,
        string sql,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
#pragma warning disable CA2100 // Review SQL queries for security vulnerabilities
        command.CommandText = sql;
#pragma warning restore CA2100

        var result = await command.ExecuteScalarAsync(cancellationToken);

        return result as string;
    }

    private sealed class SuffixNaming : IPersonalSpaceNaming
    {
        public string NameFor(string displayName) => displayName + "'s Space";
    }

    /// <summary>
    /// Names a Space longer than the column can hold, so the transaction fails at its last write.
    /// </summary>
    private sealed class OverlongNaming : IPersonalSpaceNaming
    {
        private const int LongerThanTheColumn = 400;

        public string NameFor(string displayName) => new('x', LongerThanTheColumn);
    }
}

