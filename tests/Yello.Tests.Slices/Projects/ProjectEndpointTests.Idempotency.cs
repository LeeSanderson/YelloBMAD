using System.Net;
using System.Text.Json;
using Xunit;
using Yello.Contracts;
using Yello.Contracts.ApiTokens;
using Yello.Tests.Shared;
using Yello.Tests.Slices.Accounts.RegisterAccount;

namespace Yello.Tests.Slices.Projects;

/// <summary>
/// Story 2.1 AC9 over the wire: a state-changing Space-scoped request carrying an
/// <c>Idempotency-Key</c> is applied once, the original response is replayed, and a replayed Token
/// issuance never returns the plaintext again. AR-25, AD-18, A-12, NFR-6; <c>deferred-work.md:78-83</c>,
/// <c>:253-256</c>, <c>:372</c>.
/// </summary>
/// <remarks>
/// Q6 (a) with (i) and Q7 are the recommended decisions these cases encode: records keyed
/// <c>(SpaceId, AccountId, Key)</c>, a reused key with a different request <c>422</c>, a key over 255
/// characters <c>400</c>, and the issuance replay stored without its <c>token</c> property. If dev-story
/// records a different decision, correct the case and say so in the Debug Log.
/// </remarks>
public sealed partial class ProjectEndpointTests
{
    private const string TokenPrefix = "yello_pat_";

    /// <summary>
    /// AC9 - a create replayed with the same key, by the same Account, in the same Space, with the same
    /// request, returns the original <c>201</c> (body and <c>Location</c>) and writes one row - on the
    /// Session surface and on the Token surface alike.
    /// </summary>
    /// <param name="surface">The surface the create and its replay are sent on.</param>
    /// <returns>A task.</returns>
    [Theory]
    [InlineData(IsolationSurface.Session)]
    [InlineData(IsolationSurface.ApiToken)]
    [Trait("Requirement", "AR-25")]
    [Trait("Requirement", "AD-18")]
    [Trait("Requirement", "AD-4")]
    public async Task A_create_replayed_with_the_same_key_returns_the_original_201_and_is_applied_once(IsolationSurface surface)
    {
        var database = Available();
        Actor? owner = null;
        WireResponse? original = null;
        WireResponse? replay = null;
        var key = Guid.NewGuid().ToString("D");

        await HostProcess.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);
            var token = await IssueTokenAsync(owner, owner.SpaceId);

            original = await CreateOnAsync(surface, owner, token, "Retried once", key);
            replay = await CreateOnAsync(surface, owner, token, "Retried once", key);
        });

        Assert.NotNull(owner);
        Assert.NotNull(original);
        Assert.True(original.StatusCode == HttpStatusCode.Created, $"{original.StatusCode} {original.Body}");
        Assert.Equal(original.StatusCode, replay?.StatusCode);
        Assert.Equal(original.Body, replay!.Body);
        Assert.Equal(original.Header("Location"), replay.Header("Location"));
        Assert.Single(await ProjectRowsAsync(database, owner.SpaceId));
    }

    /// <summary>
    /// AC9, Q7 - the same key with a different request is refused <c>422 idempotency-key-reused</c> and
    /// applies nothing: a silent "new request" would defeat the guarantee.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "AR-25")]
    public async Task The_same_key_with_a_different_request_is_refused_422_and_applies_nothing()
    {
        var database = Available();
        Actor? owner = null;
        WireResponse? reused = null;
        var key = Guid.NewGuid().ToString("D");

        await HostProcess.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);

            Assert.Equal(HttpStatusCode.Created, (await CreateAsync(owner, "First", key: key)).StatusCode);
            reused = await CreateAsync(owner, "Second", key: key);
        });

        Assert.NotNull(owner);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, reused?.StatusCode);
        Assert.Equal(ProblemTypes.IdempotencyKeyReused, Problem(reused!)?.Type);
        Assert.Equal(["First"], (await ProjectRowsAsync(database, owner.SpaceId)).Select(row => row.Name));
    }

    /// <summary>
    /// AC9 - "returns the original response" means the original, not the current state: a create
    /// replayed after the Project was renamed still answers the name it was created with.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "AR-25")]
    public async Task A_replay_after_a_rename_returns_the_original_response_not_the_current_state()
    {
        var database = Available();
        Actor? owner = null;
        WireResponse? original = null;
        WireResponse? replay = null;
        var key = Guid.NewGuid().ToString("D");

        await HostProcess.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);

            original = await CreateAsync(owner, "As created", key: key);
            Assert.Equal(HttpStatusCode.OK, (await RenameAsync(owner, owner.SpaceId, IdOf(original), "As renamed")).StatusCode);
            replay = await CreateAsync(owner, "As created", key: key);
        });

        Assert.NotNull(owner);
        Assert.Equal(original!.Body, replay?.Body);
        Assert.Contains("As created", replay!.Body, StringComparison.Ordinal);
        Assert.Equal(["As renamed"], (await ProjectRowsAsync(database, owner.SpaceId)).Select(row => row.Name));
    }

    /// <summary>
    /// AC9 - a record is Space- and Account-scoped: another Account using the same key in its own Space is
    /// a first use there, not a replay of the first Account's record.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "AR-25")]
    [Trait("Requirement", "NFR-1")]
    public async Task A_key_used_by_another_Account_in_its_own_Space_is_not_a_replay_of_the_first()
    {
        var database = Available();
        Actor? first = null;
        Actor? second = null;
        WireResponse? firstAnswer = null;
        WireResponse? secondAnswer = null;
        var key = Guid.NewGuid().ToString("D");

        await HostProcess.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            first = await RegisterAsync(client, database);
            second = await RegisterAsync(client, database);

            firstAnswer = await CreateAsync(first, "Same body", key: key);
            secondAnswer = await CreateAsync(second, "Same body", key: key);
        });

        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.Equal(HttpStatusCode.Created, firstAnswer?.StatusCode);
        Assert.Equal(HttpStatusCode.Created, secondAnswer?.StatusCode);
        Assert.NotEqual(IdOf(firstAnswer!), IdOf(secondAnswer!));
        Assert.Single(await ProjectRowsAsync(database, first.SpaceId));
        Assert.Single(await ProjectRowsAsync(database, second.SpaceId));
    }

    /// <summary>
    /// AC9, Task 7 - a key longer than 255 characters is refused <c>400</c> and nothing is written. Anchor:
    /// 255 characters is accepted.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Priority", "P1")]
    public async Task A_key_longer_than_255_characters_is_refused_400_and_nothing_is_written()
    {
        var database = Available();
        Actor? owner = null;
        WireResponse? tooLong = null;
        WireResponse? atLimit = null;

        await HostProcess.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);

            tooLong = await CreateAsync(owner, "Too long a key", key: new string('k', 256));
            atLimit = await CreateAsync(owner, "At the limit", key: new string('k', 255));
        });

        Assert.NotNull(owner);
        Assert.Equal(HttpStatusCode.BadRequest, tooLong?.StatusCode);
        Assert.Equal(HttpStatusCode.Created, atLimit?.StatusCode);
        Assert.Equal(["At the limit"], (await ProjectRowsAsync(database, owner.SpaceId)).Select(row => row.Name));
    }

    /// <summary>
    /// AC9, NFR-6, <c>deferred-work.md:372</c> (high) - a replayed Token issuance answers <c>201</c> with
    /// <c>Cache-Control: no-store</c> and no plaintext; exactly one Token exists; and no stored value
    /// anywhere - the idempotency record included - holds the plaintext or its secret half.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "NFR-6")]
    [Trait("Requirement", "AD-6")]
    [Trait("Requirement", "AD-18")]
    public async Task A_replayed_Token_issuance_never_returns_the_plaintext_and_no_stored_record_holds_it()
    {
        var database = Available();
        Actor? owner = null;
        WireResponse? original = null;
        WireResponse? replay = null;
        var key = Guid.NewGuid().ToString("D");

        await HostProcess.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);

            var body = JsonSerializer.Serialize(new IssueApiTokenRequest("replayed script"), JsonOptions);

            original = await owner.Session.SendWithHeadersAsync(HttpMethod.Post, ApiTokenRoutes.IssueIn(owner.SpaceId), Json(body), KeyHeader(key));
            replay = await owner.Session.SendWithHeadersAsync(HttpMethod.Post, ApiTokenRoutes.IssueIn(owner.SpaceId), Json(body), KeyHeader(key));
        });

        Assert.NotNull(owner);
        Assert.NotNull(original);
        Assert.True(original.StatusCode == HttpStatusCode.Created, $"{original.StatusCode} {original.Body}");

        string plaintext;

        using (var document = JsonDocument.Parse(original.Body))
        {
            plaintext = document.RootElement.GetProperty("token").GetString() ?? string.Empty;
        }

        Assert.StartsWith(TokenPrefix, plaintext, StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.Created, replay?.StatusCode);
        Assert.Contains("no-store", replay!.Header("Cache-Control") ?? string.Empty, StringComparison.Ordinal);
        Assert.DoesNotContain(plaintext[TokenPrefix.Length..], replay.Body, StringComparison.Ordinal);

        using (var document = JsonDocument.Parse(replay.Body))
        {
            Assert.False(document.RootElement.TryGetProperty("token", out _), "The replay carries a token property.");
        }

        await using var plain = await database.OpenWithoutSpaceContextAsync(TestContext.Current.CancellationToken);
        await using var scoped = await database.OpenForSpaceAsync(owner.SpaceId, TestContext.Current.CancellationToken);

        Assert.Equal(1, await TokenCountAsync(plain, owner.AccountId));

        var stored = (await RegistrationDatabase.ReadEveryTextValueAsync(plain, TestContext.Current.CancellationToken))
            .Concat(await RegistrationDatabase.ReadEveryTextValueAsync(scoped, TestContext.Current.CancellationToken))
            .ToList();

        Assert.NotEmpty(stored);
        Assert.DoesNotContain(stored, value => value.Contains(plaintext[TokenPrefix.Length..], StringComparison.Ordinal));
    }

    private static Task<WireResponse> CreateOnAsync(IsolationSurface surface, Actor actor, string token, string name, string key) =>
        surface == IsolationSurface.ApiToken
            ? CreateWithTokenAsync(actor, token, actor.SpaceId, name, key)
            : CreateAsync(actor, name, key: key);

    private static async Task<int> TokenCountAsync(Microsoft.Data.SqlClient.SqlConnection connection, Guid accountId)
    {
        await using var command = connection.CreateCommand();

        command.CommandText = "SELECT COUNT(*) FROM dbo.[ApiToken] WHERE AccountId = @account";
        command.Parameters.Add(new Microsoft.Data.SqlClient.SqlParameter("@account", accountId));

        return Convert.ToInt32(await command.ExecuteScalarAsync(TestContext.Current.CancellationToken), System.Globalization.CultureInfo.InvariantCulture);
    }

    private static StringContent Json(string body) => new(body, System.Text.Encoding.UTF8, "application/json");
}
