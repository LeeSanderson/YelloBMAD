using System.Net;
using Xunit;
using Yello.Contracts;
using Yello.Tests.Shared;
using static Yello.Tests.Slices.Tasks.TaskEditWire;
using static Yello.Tests.Slices.Tasks.TaskWire;
using StatusWire = Yello.Tests.Slices.Projects.ProjectStatusSliceWire;

namespace Yello.Tests.Slices.Tasks;

/// <summary>
/// A Status valid in one Project is neither accepted for a Task in another: the literal sibling-Project case the Task edit
/// and move refusals have waited for, now that two Projects of one Space can hold different sets. Until now the foreign
/// Status these cases used was another Space's.
/// </summary>
/// <remarks>
/// The sibling's Status is a real <c>StatusDefinition</c> of the same Space (owned by the sibling Project), so the composite
/// key <c>(SpaceId, StatusId)</c> would accept it: only the handler's check against the Task's own Project's effective set
/// refuses it, and the refusal must be the byte-identical <c>400 invalid-task-status</c> a random id gets.
/// </remarks>
public sealed partial class TaskEndpointTests
{
    /// <summary>
    /// Editing a Task's Status to a sibling Project's added Status is refused <c>400 invalid-task-status</c>, byte-identical
    /// to a random id's refusal, nothing changes, nothing is recorded, and neither body echoes the id. Anchor: the same Status
    /// is admitted for a Task of the sibling Project itself.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC6 - EditTaskAttributesHandler checks the Space's set, and no Project can add a Status")]
    [Trait("Requirement", "FR-20")]
    [Trait("Requirement", "FR-25")]
    [Trait("Requirement", "AD-17")]
    public async Task A_sibling_Projects_Status_is_refused_for_an_edit_identically_to_a_random_id()
    {
        var database = Available();
        Actor? owner = null;
        var taskId = Guid.Empty;
        var siblingStatus = Guid.Empty;
        TaskRow? before = null;
        TaskRow? after = null;
        WireResponse? random = null;
        WireResponse? sibling = null;
        WireResponse? anchor = null;

        await fixture.Hosts.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);

            (_, taskId) = await ProjectWithTaskAsync(owner, "Stays in its own set");

            var siblingProject = await CreateProjectAsync(owner, "Sibling");
            var siblingTask = IdOf(await CreateTaskAsync(owner, owner.SpaceId, siblingProject, "Belongs there"));

            siblingStatus = await StatusWire.AddedAsync(owner, owner.SpaceId, siblingProject, "Parked");
            before = await RowAsync(database, owner.SpaceId, taskId);
            random = await EditTaskAsync(owner, owner.SpaceId, taskId, Body(new { statusId = Guid.NewGuid() }));
            sibling = await EditTaskAsync(owner, owner.SpaceId, taskId, Body(new { statusId = siblingStatus }));
            after = await RowAsync(database, owner.SpaceId, taskId);
            anchor = await EditTaskAsync(owner, owner.SpaceId, siblingTask, Body(new { statusId = siblingStatus }));
        });

        Assert.NotNull(owner);
        AssertRefusedLikeARandomId(random, sibling, siblingStatus);
        Assert.Equal(before, after);
        Assert.Empty(await RefusalKindsAsync(database, owner.AccountId, owner.SpaceId));
        Assert.True(anchor?.StatusCode == HttpStatusCode.OK, $"{anchor?.StatusCode} {anchor?.Body}");
        await StatusWire.AssertNoTaskOutsideItsProjectsSetAsync(database, owner.SpaceId);
    }

    /// <summary>
    /// Moving a Task to a sibling Project's added Status is refused <c>400 invalid-task-status</c>, byte-identical to a random
    /// id's refusal, and the Task keeps its Status and key. Anchor: the same move inside the sibling Project is admitted.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC6 - MoveTaskHandler checks the Space's set, and no Project can add a Status")]
    [Trait("Requirement", "FR-20")]
    [Trait("Requirement", "FR-25")]
    [Trait("Requirement", "AD-17")]
    public async Task A_sibling_Projects_Status_is_refused_for_a_move_identically_to_a_random_id()
    {
        var database = Available();
        Actor? owner = null;
        var taskId = Guid.Empty;
        var siblingStatus = Guid.Empty;
        TaskRow? before = null;
        TaskRow? after = null;
        WireResponse? random = null;
        WireResponse? sibling = null;
        WireResponse? anchor = null;

        await fixture.Hosts.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);

            (_, taskId) = await ProjectWithTaskAsync(owner, "Moves only within its set");

            var siblingProject = await CreateProjectAsync(owner, "Sibling");
            var siblingTask = IdOf(await CreateTaskAsync(owner, owner.SpaceId, siblingProject, "Moves there"));

            siblingStatus = await StatusWire.AddedAsync(owner, owner.SpaceId, siblingProject, "Parked");
            before = await RowAsync(database, owner.SpaceId, taskId);
            random = await MoveWire.MoveTaskAsync(owner, owner.SpaceId, taskId, Guid.NewGuid(), afterTaskId: null);
            sibling = await MoveWire.MoveTaskAsync(owner, owner.SpaceId, taskId, siblingStatus, afterTaskId: null);
            after = await RowAsync(database, owner.SpaceId, taskId);
            anchor = await MoveWire.MoveTaskAsync(owner, owner.SpaceId, siblingTask, siblingStatus, afterTaskId: null);
        });

        Assert.NotNull(owner);
        AssertRefusedLikeARandomId(random, sibling, siblingStatus);
        Assert.Equal(before, after);
        Assert.True(anchor?.StatusCode == HttpStatusCode.OK, $"{anchor?.StatusCode} {anchor?.Body}");
        await StatusWire.AssertNoTaskOutsideItsProjectsSetAsync(database, owner.SpaceId);
    }

    private static void AssertRefusedLikeARandomId(WireResponse? random, WireResponse? sibling, Guid siblingStatus)
    {
        Assert.NotNull(random);
        Assert.NotNull(sibling);
        Assert.True(random.StatusCode == HttpStatusCode.BadRequest, $"{random.StatusCode} {random.Body}");
        Assert.Equal(ProblemTypes.InvalidTaskStatus, Problem(random)?.Type);
        Assert.Equal(random.StatusCode, sibling.StatusCode);
        Assert.Equal(random.ContentType, sibling.ContentType);
        Assert.Equal(random.Body, sibling.Body);
        Assert.DoesNotContain(siblingStatus.ToString("D"), sibling.Body, StringComparison.OrdinalIgnoreCase);
    }
}
