using System.Net;
using Xunit;
using Yello.Contracts;
using Yello.Tests.Shared;
using static Yello.Tests.Slices.Tasks.TaskEditWire;
using static Yello.Tests.Slices.Tasks.TaskWire;

namespace Yello.Tests.Slices.Tasks;

/// <summary>
/// Story 2.3 Task 7 over the wire: <c>PATCH …/tasks/{taskId}</c> is a <c>{spaceId}</c> state-changing route, so
/// the pipeline's <c>Idempotency-Key</c> behaviour applies unchanged - the original answer replayed byte for
/// byte without re-applying, a reused key with another body refused, records scoped to (Space, Account, key).
/// Each case on both surfaces (AD-4). AC10; AR-25, AD-18, P-10. Written red by the story 2.3 ATDD run.
/// </summary>
public sealed partial class TaskEndpointTests
{
    /// <summary>
    /// AC10 - an edit replayed with the same key returns the original <c>200</c> byte for byte and does not
    /// re-apply: replayed after a <b>different</b> edit by another key, it still answers the first edit's body
    /// and the Task keeps the later title.
    /// </summary>
    /// <param name="surface">The surface every request is sent on.</param>
    /// <returns>A task.</returns>
    [Theory(Skip = "red: AC10 - no Task PATCH route yet (Task 5, Task 7)")]
    [InlineData(IsolationSurface.Session)]
    [InlineData(IsolationSurface.ApiToken)]
    [Trait("Requirement", "AR-25")]
    [Trait("Requirement", "AD-18")]
    [Trait("Requirement", "AD-4")]
    public async Task An_edit_replayed_with_the_same_key_returns_the_original_200_and_is_not_reapplied(IsolationSurface surface)
    {
        var database = Available();
        Actor? owner = null;
        var taskId = Guid.Empty;
        WireResponse? original = null;
        WireResponse? later = null;
        WireResponse? replay = null;
        var key = Guid.NewGuid().ToString("D");

        await HostProcess.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);
            var token = await IssueTokenAsync(owner, owner.SpaceId);

            (_, taskId) = await ProjectWithTaskAsync(owner, "Before");

            original = await EditTaskOnAsync(surface, owner, token, taskId, Body(new { title = "First edit" }), key);
            later = await EditTaskOnAsync(surface, owner, token, taskId, Body(new { title = "Second edit" }), Guid.NewGuid().ToString("D"));
            replay = await EditTaskOnAsync(surface, owner, token, taskId, Body(new { title = "First edit" }), key);
        });

        Assert.NotNull(owner);
        Assert.NotNull(original);
        Assert.True(original.StatusCode == HttpStatusCode.OK, $"{original.StatusCode} {original.Body}");
        Assert.Equal(HttpStatusCode.OK, later?.StatusCode);
        Assert.Equal(original.StatusCode, replay?.StatusCode);
        Assert.Equal(original.Body, replay!.Body);
        Assert.Equal("Second edit", (await RowAsync(database, owner.SpaceId, taskId))?.Title);
    }

    /// <summary>
    /// AC10, AR-25 - the same key with a different body is refused <c>422 idempotency-key-reused</c> and changes
    /// nothing.
    /// </summary>
    /// <param name="surface">The surface both requests are sent on.</param>
    /// <returns>A task.</returns>
    [Theory(Skip = "red: AC10 - no Task PATCH route yet (Task 5, Task 7)")]
    [InlineData(IsolationSurface.Session)]
    [InlineData(IsolationSurface.ApiToken)]
    [Trait("Requirement", "AR-25")]
    public async Task The_same_key_with_a_different_edit_is_refused_422_and_changes_nothing(IsolationSurface surface)
    {
        var database = Available();
        Actor? owner = null;
        var taskId = Guid.Empty;
        WireResponse? reused = null;
        var key = Guid.NewGuid().ToString("D");

        await HostProcess.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);
            var token = await IssueTokenAsync(owner, owner.SpaceId);

            (_, taskId) = await ProjectWithTaskAsync(owner, "Before");

            Assert.Equal(HttpStatusCode.OK, (await EditTaskOnAsync(surface, owner, token, taskId, Body(new { title = "First" }), key)).StatusCode);
            reused = await EditTaskOnAsync(surface, owner, token, taskId, Body(new { title = "Second" }), key);
        });

        Assert.NotNull(owner);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, reused?.StatusCode);
        Assert.Equal(ProblemTypes.IdempotencyKeyReused, Problem(reused!)?.Type);
        Assert.Equal("First", (await RowAsync(database, owner.SpaceId, taskId))?.Title);
    }

    /// <summary>
    /// AC10, NFR-1 - another Account's key does not replay: B using A's key on B's own Task in B's own Space is a
    /// first use there, and answers B's Task.
    /// </summary>
    /// <param name="surface">The surface both edits are sent on.</param>
    /// <returns>A task.</returns>
    [Theory(Skip = "red: AC10 - no Task PATCH route yet (Task 5, Task 7)")]
    [InlineData(IsolationSurface.Session)]
    [InlineData(IsolationSurface.ApiToken)]
    [Trait("Requirement", "AR-25")]
    [Trait("Requirement", "NFR-1")]
    public async Task A_key_used_by_another_Account_on_its_own_Task_does_not_replay_the_first(IsolationSurface surface)
    {
        var database = Available();
        Actor? first = null;
        Actor? second = null;
        var secondTask = Guid.Empty;
        WireResponse? firstAnswer = null;
        WireResponse? secondAnswer = null;
        var key = Guid.NewGuid().ToString("D");

        await HostProcess.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            first = await RegisterAsync(client, database);
            second = await RegisterAsync(client, database);
            var firstToken = await IssueTokenAsync(first, first.SpaceId);
            var secondToken = await IssueTokenAsync(second, second.SpaceId);
            var (_, firstTask) = await ProjectWithTaskAsync(first, "A's");

            (_, secondTask) = await ProjectWithTaskAsync(second, "B's");

            firstAnswer = await EditTaskOnAsync(surface, first, firstToken, firstTask, Body(new { title = "Same body" }), key);
            secondAnswer = await EditTaskOnAsync(surface, second, secondToken, secondTask, Body(new { title = "Same body" }), key);
        });

        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.Equal(HttpStatusCode.OK, firstAnswer?.StatusCode);
        Assert.Equal(HttpStatusCode.OK, secondAnswer?.StatusCode);
        Assert.Contains(secondTask.ToString("D"), secondAnswer!.Body, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("Same body", (await RowAsync(database, second.SpaceId, secondTask))?.Title);
    }
}
