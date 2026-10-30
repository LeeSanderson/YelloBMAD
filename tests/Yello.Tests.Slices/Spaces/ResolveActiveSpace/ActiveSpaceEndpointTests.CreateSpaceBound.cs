using System.Data.Common;
using System.Net;
using Microsoft.Data.SqlClient;
using Xunit;
using Yello.Contracts;
using Yello.Tests.Shared;

namespace Yello.Tests.Slices.Spaces.ResolveActiveSpace;

/// <summary>
/// Story 3.1 over the wire: the Spaces-per-Account bound (AC6, AC11, AC12; NFR-8, AR-30, AD-25) and the
/// Account-scoped <c>Idempotency-Key</c> on creation (Q2 (a), decided by Lee 2026-10-29; AR-25, AD-18).
/// </summary>
/// <remarks>
/// <para>
/// <b>RED-PHASE SCAFFOLDS (story 3.1 ATDD run, 2026-10-29).</b> All skipped; see
/// <c>ActiveSpaceEndpointTests.CreateSpace.cs</c> for the seeding constraints.
/// </para>
/// <para>
/// <b>The race is held, never hoped for</b> (<c>TESTING-CONVENTIONS.md</c>, stories 2.2 and 2.9). The test takes
/// the product's own application lock for the Account - <c>Yello/SpacesPerAccount/{accountId:D}</c>, the name
/// Task 4 gives <c>ApplicationLocks.TakeSpacesPerAccountAsync</c> - on a connection of its own, sends both
/// creations, waits until both are blocked on a lock (<c>TaskWire.WaitUntilBlockedOnALockAsync</c>), then
/// commits. <b>If dev-story spells the lock differently, correct the one constant
/// <see cref="SpacesPerAccountLockOf"/> and record it in the Debug Log.</b> The hold needs no session context:
/// an application lock is not a row.
/// </para>
/// <para>
/// <b>Q1 (a) is what these cases rest on</b> (decided by Lee): one named creation unit of work holding both keys,
/// so the count and the inserts share one transaction. Under the rejected (b), two units of work, the race case
/// fails: both counts read 49.
/// </para>
/// </remarks>
public sealed partial class ActiveSpaceEndpointTests
{
    /// <summary>
    /// AC6, AC12, NFR-8, AD-25 - an Account holding 49 Memberships (its Personal Space and 48 more, at every Role,
    /// owned or not) creates the 50th (the anchor); the 51st is refused <c>409 bound-reached</c> with
    /// <c>bound: spaces-per-account</c> and <c>limit: 50</c>; it still holds 50, no Space of the refused name exists,
    /// and a bound refusal records no <c>AccessRefusal</c> (it is neither kind).
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC6 - POST /api/v1/spaces and the SpacesPerAccount count do not exist yet (story 3.1 Task 4)")]
    [Trait("Requirement", "NFR-8")]
    [Trait("Requirement", "AR-30")]
    [Trait("Requirement", "AD-25")]
    public async Task The_51st_Space_is_refused_409_bound_reached_and_the_Account_still_holds_50()
    {
        var database = Available();
        var address = RandomAddress();
        var refusedName = "The fifty-first " + Suffix();
        WireResponse? fiftieth = null;
        WireResponse? fiftyFirst = null;
        var accountId = Guid.Empty;

        await HostProcess.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            var session = await SignedInAsync(client, address);
            accountId = await AccountIdOfAsync(database, address);
            await SeedMembershipsAsync(database, accountId, SpacesPerAccount - 2);

            await session.FetchAntiforgeryTokenAsync();
            fiftieth = await CreateSpaceAsync(session, "The fiftieth " + Suffix());
            fiftyFirst = await CreateSpaceAsync(session, refusedName);
        });

        Assert.True(fiftieth?.StatusCode == HttpStatusCode.Created, $"Anchor: {fiftieth?.StatusCode} {fiftieth?.Body}");
        Assert.True(fiftyFirst?.StatusCode == HttpStatusCode.Conflict, $"{fiftyFirst?.StatusCode} {fiftyFirst?.Body}");

        var problem = System.Text.Json.JsonSerializer.Deserialize<ProblemResponse>(fiftyFirst!.Body, JsonOptions);

        Assert.Equal(ProblemTypes.BoundReached, problem?.Type);
        Assert.Equal(SpacesPerAccountCode, problem?.Bound);
        Assert.Equal(SpacesPerAccount, problem?.Limit);

        // Inside the same transaction (AD-25): no Space, no Membership, no Status of the refused creation.
        Assert.Equal(SpacesPerAccount, await MembershipCountOfAsync(database, accountId));
        Assert.Empty(await SpaceIdsNamedAsync(database, refusedName));
        Assert.Empty(await RefusalsOfAsync(database, accountId));
    }

    /// <summary>
    /// AC12 - the count is live, never a stored counter: at 50 a creation is refused (the anchor); once one of the
    /// Account's Memberships is gone, the next creation is admitted.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC12 - POST /api/v1/spaces and the SpacesPerAccount count do not exist yet (story 3.1 Task 4)")]
    [Trait("Requirement", "NFR-8")]
    [Trait("Requirement", "AD-25")]
    public async Task A_Membership_the_Account_no_longer_holds_does_not_count_toward_the_bound()
    {
        var database = Available();
        var address = RandomAddress();
        WireResponse? atTheBound = null;
        WireResponse? afterLeaving = null;

        await HostProcess.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            var session = await SignedInAsync(client, address);
            var accountId = await AccountIdOfAsync(database, address);
            var seeded = await SeedMembershipsAsync(database, accountId, SpacesPerAccount - 1);

            await session.FetchAntiforgeryTokenAsync();
            atTheBound = await CreateSpaceAsync(session, "Refused at fifty " + Suffix());

            // An Admin Membership (index 1 of the Role cycle): leaving a Space is not a 3.1 surface, so the row goes by SQL.
            await RemoveMembershipAsync(database, seeded[1], accountId);

            afterLeaving = await CreateSpaceAsync(session, "Admitted at forty-nine " + Suffix());
        });

        Assert.Equal(HttpStatusCode.Conflict, atTheBound?.StatusCode);
        Assert.True(afterLeaving?.StatusCode == HttpStatusCode.Created, $"{afterLeaving?.StatusCode} {afterLeaving?.Body}");
    }

    /// <summary>
    /// AC11, AD-25 - two creations by one Account holding 49 Memberships, made to overlap on the Account's
    /// application lock, end with exactly one <c>201</c> and one <c>409 bound-reached</c>, and the Account holds 50.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC11 - the SpacesPerAccount count and its application lock do not exist yet (story 3.1 Task 4)")]
    [Trait("Requirement", "AR-30")]
    [Trait("Requirement", "AD-25")]
    public async Task Two_concurrent_creations_at_49_yield_exactly_one_201_and_one_409()
    {
        var database = Available();
        var cancellationToken = TestContext.Current.CancellationToken;
        var address = RandomAddress();
        var accountId = Guid.Empty;
        WireResponse[] answers = [];

        await HostProcess.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            // Two Sessions of one Account, so the two requests share no WireSession state.
            var first = await SignedInAsync(client, address);
            var second = await SignedInWithoutRegisteringAsync(client, address);

            accountId = await AccountIdOfAsync(database, address);
            await SeedMembershipsAsync(database, accountId, SpacesPerAccount - 2);

            await first.FetchAntiforgeryTokenAsync();
            await second.FetchAntiforgeryTokenAsync();

            await using var holder = await database.OpenWithoutSpaceContextAsync(cancellationToken);
            await using var observer = await database.OpenWithoutSpaceContextAsync(cancellationToken);
            await using var held = await holder.BeginTransactionAsync(cancellationToken);

            await TakeApplicationLockAsync(holder, held, SpacesPerAccountLockOf(accountId), cancellationToken);

            var racing = Task.WhenAll(
                CreateSpaceAsync(first, "Racer one " + Suffix()),
                CreateSpaceAsync(second, "Racer two " + Suffix()));

            await Tasks.TaskWire.WaitUntilBlockedOnALockAsync(observer, 2, racing, cancellationToken);

            await held.CommitAsync(cancellationToken);

            answers = await racing;
        });

        Assert.Equal(
            [HttpStatusCode.Created, HttpStatusCode.Conflict],
            answers.Select(answer => answer.StatusCode).Order());
        Assert.Equal(ProblemTypes.BoundReached, ProblemType(answers.Single(answer => answer.StatusCode == HttpStatusCode.Conflict)));
        Assert.Equal(SpacesPerAccount, await MembershipCountOfAsync(database, accountId));
    }

    /// <summary>
    /// AC11 - a creation by another Account never waits on this Account's lock: with A's lock held, B's creation
    /// answers <c>201</c> without anything being released. Anchor: A's own creation, sent next, does wait on it -
    /// so the held lock is the product's, and B's not waiting is not a misnamed hold.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC11 - the SpacesPerAccount count and its application lock do not exist yet (story 3.1 Task 4)")]
    [Trait("Requirement", "AD-25")]
    [Trait("Requirement", "NFR-1")]
    public async Task Another_Accounts_creation_never_waits_on_this_Accounts_lock()
    {
        var database = Available();
        var cancellationToken = TestContext.Current.CancellationToken;
        var (holderAddress, otherAddress) = (RandomAddress(), RandomAddress());
        WireResponse? others = null;
        WireResponse? own = null;

        await HostProcess.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            var a = await SignedInAsync(client, holderAddress);
            var b = await SignedInAsync(client, otherAddress);
            var accountA = await AccountIdOfAsync(database, holderAddress);

            await a.FetchAntiforgeryTokenAsync();
            await b.FetchAntiforgeryTokenAsync();

            await using var holder = await database.OpenWithoutSpaceContextAsync(cancellationToken);
            await using var observer = await database.OpenWithoutSpaceContextAsync(cancellationToken);
            await using var held = await holder.BeginTransactionAsync(cancellationToken);

            await TakeApplicationLockAsync(holder, held, SpacesPerAccountLockOf(accountA), cancellationToken);

            var othersCreation = CreateSpaceAsync(b, "Not waiting " + Suffix());

            await Tasks.LabelWire.WaitUntilBlockedOrAnsweredAsync(observer, 1, othersCreation, cancellationToken);

            Assert.True(othersCreation.IsCompleted, "Another Account's creation waited on a lock while A's was held.");
            others = await othersCreation;

            var ownCreation = CreateSpaceAsync(a, "Waiting " + Suffix());

            await Tasks.TaskWire.WaitUntilBlockedOnALockAsync(observer, 1, ownCreation, cancellationToken);

            await held.CommitAsync(cancellationToken);

            own = await ownCreation;
        });

        Assert.True(others?.StatusCode == HttpStatusCode.Created, $"{others?.StatusCode} {others?.Body}");
        Assert.True(own?.StatusCode == HttpStatusCode.Created, $"Anchor: {own?.StatusCode} {own?.Body}");
    }

    /// <summary>
    /// Q2 (a), AR-25, AD-18 - a creation replayed with the same <c>Idempotency-Key</c> returns the original
    /// status, body and <c>Location</c>, and one Space exists.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC9 - the Account-scoped idempotency record does not exist yet (story 3.1 Task 4, Q2 (a))")]
    [Trait("Requirement", "AR-25")]
    [Trait("Requirement", "AD-18")]
    public async Task A_creation_replayed_with_its_key_returns_the_original_answer_and_creates_one_Space()
    {
        var database = Available();
        var address = RandomAddress();
        var name = "Replayed " + Suffix();
        var key = Guid.NewGuid().ToString("D");
        WireResponse? original = null;
        WireResponse? replay = null;

        await HostProcess.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            var session = await SignedInAsync(client, address);
            await session.FetchAntiforgeryTokenAsync();

            original = await CreateSpaceAsync(session, name, key);
            replay = await CreateSpaceAsync(session, name, key);
        });

        Assert.Equal(HttpStatusCode.Created, original?.StatusCode);
        Assert.Equal(HttpStatusCode.Created, replay?.StatusCode);
        Assert.Equal(original!.Body, replay!.Body);
        Assert.Equal(original.Header("Location"), replay.Header("Location"));
        Assert.Single(await SpaceIdsNamedAsync(database, name));
    }

    /// <summary>
    /// Q2 (a), P-10 - the replay comes before the bound: replaying the key of the 50th creation, once the Account
    /// is full, returns that creation's <c>201</c>, not a <c>409</c>; a fresh key is refused (the anchor).
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC9 - the Account-scoped idempotency record does not exist yet (story 3.1 Task 4, Q2 (a))")]
    [Trait("Requirement", "AR-25")]
    [Trait("Requirement", "AD-18")]
    [Trait("Requirement", "AD-25")]
    public async Task Replaying_the_key_of_the_50th_creation_returns_its_201_not_the_bound_409()
    {
        var database = Available();
        var address = RandomAddress();
        var name = "The fiftieth " + Suffix();
        var key = Guid.NewGuid().ToString("D");
        var accountId = Guid.Empty;
        WireResponse? original = null;
        WireResponse? replay = null;
        WireResponse? fresh = null;

        await HostProcess.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            var session = await SignedInAsync(client, address);
            accountId = await AccountIdOfAsync(database, address);
            await SeedMembershipsAsync(database, accountId, SpacesPerAccount - 2);

            await session.FetchAntiforgeryTokenAsync();
            original = await CreateSpaceAsync(session, name, key);
            replay = await CreateSpaceAsync(session, name, key);
            fresh = await CreateSpaceAsync(session, name, Guid.NewGuid().ToString("D"));
        });

        Assert.Equal(HttpStatusCode.Created, original?.StatusCode);
        Assert.Equal(HttpStatusCode.Created, replay?.StatusCode);
        Assert.Equal(original!.Body, replay!.Body);
        Assert.Equal(HttpStatusCode.Conflict, fresh?.StatusCode);
        Assert.Equal(SpacesPerAccount, await MembershipCountOfAsync(database, accountId));
    }

    /// <summary>
    /// Q2 (a), AD-18 - the same key with a different body is refused <c>422 idempotency-key-reused</c> and nothing
    /// is applied; another Account's same key is a first use of its own (keyed <c>(AccountId, Key)</c>).
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC9 - the Account-scoped idempotency record does not exist yet (story 3.1 Task 4, Q2 (a))")]
    [Trait("Requirement", "AR-25")]
    [Trait("Requirement", "AD-18")]
    public async Task A_key_reused_with_another_body_is_refused_422_and_another_Accounts_same_key_is_a_first_use()
    {
        var database = Available();
        var key = Guid.NewGuid().ToString("D");
        var (firstName, secondName, othersName) = ("First " + Suffix(), "Second " + Suffix(), "Other's " + Suffix());
        WireResponse? first = null;
        WireResponse? reused = null;
        WireResponse? others = null;

        await HostProcess.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            var session = await SignedInAsync(client, RandomAddress());
            var other = await SignedInAsync(client, RandomAddress());
            await session.FetchAntiforgeryTokenAsync();
            await other.FetchAntiforgeryTokenAsync();

            first = await CreateSpaceAsync(session, firstName, key);
            reused = await CreateSpaceAsync(session, secondName, key);
            others = await CreateSpaceAsync(other, othersName, key);
        });

        Assert.Equal(HttpStatusCode.Created, first?.StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, reused?.StatusCode);
        Assert.Equal(ProblemTypes.IdempotencyKeyReused, ProblemType(reused!));
        Assert.Empty(await SpaceIdsNamedAsync(database, secondName));

        Assert.Equal(HttpStatusCode.Created, others?.StatusCode);
        Assert.NotEqual(CreatedSpace(first!).Id, CreatedSpace(others!).Id);
        Assert.Single(await SpaceIdsNamedAsync(database, othersName));
    }

    /// <summary>
    /// The product's application lock for an Account's Spaces-per-Account count, as story 3.1 Task 4 names it.
    /// </summary>
    private static string SpacesPerAccountLockOf(Guid accountId) => $"Yello/SpacesPerAccount/{accountId:D}";

    private static async Task TakeApplicationLockAsync(SqlConnection holder, DbTransaction held, string resource, CancellationToken cancellationToken)
    {
        await using DbCommand command = holder.CreateCommand();

        command.Transaction = held;
        command.CommandText = "EXEC sp_getapplock @Resource = @resource, @LockMode = 'Exclusive', @LockOwner = 'Transaction'";
        command.Parameters.Add(new SqlParameter("@resource", resource));

        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
