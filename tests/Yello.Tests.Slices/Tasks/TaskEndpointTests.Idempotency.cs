using System.Net;
using Xunit;
using Yello.Contracts;
using Yello.Tests.Shared;
using static Yello.Tests.Slices.Tasks.TaskWire;

namespace Yello.Tests.Slices.Tasks;

/// <summary>
/// Story 2.2 Task 6 over the wire: <c>POST …/tasks</c> is a <c>{spaceId}</c> state-changing route, so 2.1's
/// <c>Idempotency-Key</c> behaviour applies to it unchanged - one row, the original answer replayed, a
/// reused key with another request refused, and records scoped to (Space, Account, key). Each case runs on
/// both surfaces (AD-4). AR-25, AD-18, P-10.
/// </summary>
public sealed partial class TaskEndpointTests
{
    /// <summary>
    /// AC2, AR-25 - a Task creation replayed with the same key returns the original <c>201</c> - body and
    /// <c>Location</c> byte for byte - and writes one row.
    /// </summary>
    /// <param name="surface">The surface both requests are sent on.</param>
    /// <returns>A task.</returns>
    [Theory]
    [InlineData(IsolationSurface.Session)]
    [InlineData(IsolationSurface.ApiToken)]
    [Trait("Requirement", "AR-25")]
    [Trait("Requirement", "AD-18")]
    [Trait("Requirement", "AD-4")]
    public async Task A_Task_creation_replayed_with_the_same_key_returns_the_original_201_and_is_applied_once(IsolationSurface surface)
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
            var projectId = await CreateProjectAsync(owner, "Retries");

            original = await CreateTaskOnAsync(surface, owner, token, projectId, "Sent twice", key);
            replay = await CreateTaskOnAsync(surface, owner, token, projectId, "Sent twice", key);
        });

        Assert.NotNull(owner);
        Assert.NotNull(original);
        Assert.True(original.StatusCode == HttpStatusCode.Created, $"{original.StatusCode} {original.Body}");
        Assert.Equal(original.StatusCode, replay?.StatusCode);
        Assert.Equal(original.Body, replay!.Body);
        Assert.Equal(original.Header("Location"), replay.Header("Location"));
        Assert.Single(await TaskRowsAsync(database, owner.SpaceId));
    }

    /// <summary>
    /// AR-25, 2.1 Q7 - the same key with a different title is refused <c>422 idempotency-key-reused</c> and
    /// applies nothing.
    /// </summary>
    /// <param name="surface">The surface both requests are sent on.</param>
    /// <returns>A task.</returns>
    [Theory]
    [InlineData(IsolationSurface.Session)]
    [InlineData(IsolationSurface.ApiToken)]
    [Trait("Requirement", "AR-25")]
    public async Task The_same_key_with_a_different_title_is_refused_422_and_applies_nothing(IsolationSurface surface)
    {
        var database = Available();
        Actor? owner = null;
        WireResponse? reused = null;
        var key = Guid.NewGuid().ToString("D");

        await HostProcess.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);
            var token = await IssueTokenAsync(owner, owner.SpaceId);
            var projectId = await CreateProjectAsync(owner, "Reuse");

            Assert.Equal(HttpStatusCode.Created, (await CreateTaskOnAsync(surface, owner, token, projectId, "First", key)).StatusCode);
            reused = await CreateTaskOnAsync(surface, owner, token, projectId, "Second", key);
        });

        Assert.NotNull(owner);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, reused?.StatusCode);
        Assert.Equal(ProblemTypes.IdempotencyKeyReused, Problem(reused!)?.Type);
        Assert.Equal(["First"], (await TaskRowsAsync(database, owner.SpaceId)).Select(row => row.Title));
    }

    /// <summary>
    /// AR-25, NFR-1 - a record is Space- and Account-scoped: Account B using A's key in B's own Space is a
    /// first use there, not a replay of A's Task.
    /// </summary>
    /// <param name="surface">The surface both creations are sent on.</param>
    /// <returns>A task.</returns>
    [Theory]
    [InlineData(IsolationSurface.Session)]
    [InlineData(IsolationSurface.ApiToken)]
    [Trait("Requirement", "AR-25")]
    [Trait("Requirement", "NFR-1")]
    public async Task A_key_used_by_another_Account_in_its_own_Space_does_not_replay_the_first(IsolationSurface surface)
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
            var firstToken = await IssueTokenAsync(first, first.SpaceId);
            var secondToken = await IssueTokenAsync(second, second.SpaceId);
            var firstProject = await CreateProjectAsync(first, "A's");
            var secondProject = await CreateProjectAsync(second, "B's");

            firstAnswer = await CreateTaskOnAsync(surface, first, firstToken, firstProject, "Same body", key);
            secondAnswer = await CreateTaskOnAsync(surface, second, secondToken, secondProject, "Same body", key);
        });

        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.Equal(HttpStatusCode.Created, firstAnswer?.StatusCode);
        Assert.Equal(HttpStatusCode.Created, secondAnswer?.StatusCode);
        Assert.NotEqual(IdOf(firstAnswer!), IdOf(secondAnswer!));
        Assert.Single(await TaskRowsAsync(database, first.SpaceId));
        Assert.Single(await TaskRowsAsync(database, second.SpaceId));
    }
}
