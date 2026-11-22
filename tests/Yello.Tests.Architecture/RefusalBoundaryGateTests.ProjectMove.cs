using System.Reflection;
using Microsoft.AspNetCore.Routing;
using Xunit;
using Yello.Domain.Memberships;
using Yello.Host.Spaces;

namespace Yello.Tests.Architecture;

/// <summary>
/// The structural gates of moving a Task into another Project: one <c>POST …/tasks/{taskId}/project</c> requiring
/// <c>ManageTasks</c> on both surfaces and counting no bound in the pipeline; its request names a Project and an optional
/// Status and nothing else; no Task request names a Space; the bad-destination refusal has a stable type; every Task answer
/// states its Project; and the move is one sorted lock port and one slice.
/// </summary>
/// <remarks>
/// <para>
/// <b>The subjects are found by name</b>, so a missing or renamed member fails here with the name it lost rather than as a
/// compile error in an unrelated gate. The route pattern stays a literal: it is what the gate asserts.
/// </para>
/// <para>
/// <b>The Space-member scan has an oracle</b> (<see cref="The_Space_member_scan_reports_a_planted_Space_member"/>), so an
/// empty result is never a scan that saw nothing.
/// </para>
/// </remarks>
public sealed partial class RefusalBoundaryGateTests
{
    private const string TaskProjectMovePattern = TaskItemPattern + "/project";

    /// <summary>
    /// <c>POST</c> is mapped on the move-to-Project route requiring <c>ManageTasks</c>, not Session-only (it writes no client
    /// position), consulting no bound in the pipeline (its destination is in the body, and a pipeline lock taken before the
    /// slice's sorted pair could deadlock two opposite moves), and carrying exactly the Space and the Task in its path.
    /// </summary>
    [Fact]
    [Trait("Requirement", "FR-41")]
    [Trait("Requirement", "FR-35")]
    [Trait("Requirement", "AD-4")]
    [Trait("Requirement", "AD-25")]
    public void The_move_to_another_Project_route_requires_ManageTasks_serves_both_surfaces_and_counts_no_bound_in_the_pipeline()
    {
        var route = Assert.Single(RouteTable(), endpoint => endpoint.RoutePattern.RawText == TaskProjectMovePattern);

        Assert.Equal(["POST"], route.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods ?? []);
        Assert.Equal(Capability.ManageTasks, route.Metadata.GetMetadata<RequiredCapability>()?.Capability);
        Assert.DoesNotContain(route.Metadata, metadata => metadata.GetType().FullName == SessionOnlyMarkerName);
        Assert.Null(route.Metadata.GetMetadata<RequiredBound>());
        Assert.True(CarriesSpaceId(route), "The move-to-Project route does not carry {spaceId}.");
        Assert.Equal(["spaceId", "taskId"], route.RoutePattern.Parameters.Select(parameter => parameter.Name).Order(StringComparer.Ordinal));
    }

    /// <summary>
    /// <c>TaskRoutes</c> names the move-to-Project template once (<c>ProjectMove</c>), and <c>ProjectMoveOf</c> builds that
    /// path under the Task's own path for a Space and a Task.
    /// </summary>
    [Fact]
    [Trait("Requirement", "AR-35")]
    public void TaskRoutes_names_the_move_to_Project_template_once()
    {
        var routes = typeof(Yello.Contracts.Tasks.TaskRoutes);
        var template = routes.GetProperty("ProjectMove", BindingFlags.Public | BindingFlags.Static);
        var pathOf = routes.GetMethod("ProjectMoveOf", BindingFlags.Public | BindingFlags.Static, [typeof(Guid), typeof(Guid)]);

        Assert.True(template is not null, "TaskRoutes has no static ProjectMove property.");
        Assert.True(pathOf is not null, "TaskRoutes has no static ProjectMoveOf(Guid, Guid).");
        Assert.Equal(TaskProjectMovePattern, template.GetValue(null));

        var (spaceId, taskId) = (Guid.NewGuid(), Guid.NewGuid());

        Assert.Equal(Yello.Contracts.Tasks.TaskRoutes.TaskOf(spaceId, taskId) + "/project", pathOf.Invoke(null, [spaceId, taskId]));
    }

    /// <summary>
    /// The move-to-Project request names the destination Project and an optional destination Status and nothing else: no
    /// position, no neighbour, no Space. It is the only Task request that names a Project.
    /// </summary>
    [Fact]
    [Trait("Requirement", "FR-41")]
    [Trait("Requirement", "FR-19")]
    [Trait("Requirement", "AD-15")]
    public void The_move_to_Project_request_names_a_Project_and_an_optional_Status_and_is_the_only_Task_request_naming_a_Project()
    {
        var request = ProductionAssemblies.Contracts.GetType(TaskContractsNamespace + ".MoveTaskToProjectRequest");

        Assert.True(request is not null, "Yello.Contracts.Tasks.MoveTaskToProjectRequest does not exist.");
        Assert.Equal(
            ["ProjectId:Guid", "StatusId:Nullable`1"],
            request.GetProperties(BindingFlags.Public | BindingFlags.Instance).Select(property => $"{property.Name}:{property.PropertyType.Name}").Order(StringComparer.Ordinal));
        Assert.Equal(typeof(Guid?), request.GetProperty("StatusId")?.PropertyType);

        var namingAProject = TaskRequests()
            .Where(type => Array.Exists(type.GetProperties(BindingFlags.Public | BindingFlags.Instance), property => property.Name.Contains("Project", StringComparison.Ordinal)))
            .Select(type => type.Name);

        Assert.Equal(["MoveTaskToProjectRequest"], namingAProject);
    }

    /// <summary>
    /// No Task request contract carries a member naming a Space, so no request can ask for a Task to change Space.
    /// The vacuity guard is that the scan finds the Task requests that exist.
    /// </summary>
    [Fact]
    [Trait("Requirement", "FR-19")]
    [Trait("Requirement", "AD-2")]
    public void No_Task_request_contract_carries_a_Space_member()
    {
        var requests = TaskRequests();

        Assert.True(requests.Count >= 4, $"The scan found {requests.Count} Task requests; the contract holds at least four.");
        Assert.Equal([], SpaceMembersOf(requests));
    }

    /// <summary>
    /// The oracle for <see cref="No_Task_request_contract_carries_a_Space_member"/>: the same rule, run over a planted type,
    /// reports its Space member.
    /// </summary>
    [Fact]
    [Trait("Requirement", "FR-19")]
    public void The_Space_member_scan_reports_a_planted_Space_member()
    {
        Assert.Equal([$"{nameof(PlantedSpaceRequest)}.{nameof(PlantedSpaceRequest.TargetSpaceId)}"], SpaceMembersOf([typeof(PlantedSpaceRequest)]));
    }

    /// <summary>
    /// The refusal for a destination that is not another Project of the Space is stated once in the contract:
    /// <c>ProblemTypes.InvalidTaskProject</c> is <c>urn:yello:problem:invalid-task-project</c>.
    /// </summary>
    [Fact]
    [Trait("Requirement", "AR-34")]
    public void The_bad_destination_refusal_is_a_stable_problem_type()
    {
        var type = typeof(Yello.Contracts.ProblemTypes).GetProperty("InvalidTaskProject", BindingFlags.Public | BindingFlags.Static);

        Assert.True(type is not null, "ProblemTypes has no static InvalidTaskProject.");
        Assert.Equal("urn:yello:problem:invalid-task-project", type.GetValue(null));
    }

    /// <summary>
    /// Every Task answer states the Task's Project: <c>TaskResponse</c> carries a <c>Guid</c> <c>ProjectId</c>, appended, since
    /// the Task's own URL holds no Project.
    /// </summary>
    [Fact]
    [Trait("Requirement", "AD-19")]
    [Trait("Requirement", "FR-35")]
    public void TaskResponse_states_the_Tasks_Project()
    {
        Assert.Equal(typeof(Guid), typeof(Yello.Contracts.Tasks.TaskResponse).GetProperty("ProjectId")?.PropertyType);
    }

    /// <summary>
    /// A move between Projects is one Domain port that takes several Projects' locks at once - so both are taken in one order,
    /// never by two single calls - and one slice, <c>Yello.Application.Tasks.MoveTaskToProject.MoveTaskToProjectHandler</c>, a
    /// sealed class with a <c>HandleAsync</c>.
    /// </summary>
    [Fact]
    [Trait("Requirement", "AR-3")]
    [Trait("Requirement", "AD-17")]
    public void A_move_to_another_Project_is_one_sorted_lock_port_and_one_slice()
    {
        var port = ProductionAssemblies.Domain.GetType("Yello.Domain.Tasks.ITaskStore");

        Assert.True(port is not null, "Yello.Domain.Tasks.ITaskStore does not exist.");
        Assert.Contains(
            port.GetMethods(),
            method => method.ReturnType == typeof(Task)
                && method.GetParameters().Select(parameter => parameter.ParameterType).SequenceEqual([typeof(IReadOnlyCollection<Guid>), typeof(CancellationToken)]));

        var handler = ProductionAssemblies.Application.GetType("Yello.Application.Tasks.MoveTaskToProject.MoveTaskToProjectHandler");

        Assert.True(handler is not null, "Yello.Application.Tasks.MoveTaskToProject.MoveTaskToProjectHandler does not exist.");
        Assert.True(handler.IsSealed, "MoveTaskToProjectHandler is not sealed.");
        Assert.Contains(handler.GetMethods(BindingFlags.Public | BindingFlags.Instance), method => method.Name == "HandleAsync");
    }

    private static List<Type> TaskRequests() =>
    [
        .. ProductionAssemblies.Contracts.GetTypes()
            .Where(type => type.Namespace == TaskContractsNamespace && type.Name.EndsWith("Request", StringComparison.Ordinal))
            .OrderBy(type => type.Name, StringComparer.Ordinal),
    ];

    private static List<string> SpaceMembersOf(IEnumerable<Type> types) =>
    [
        .. types.SelectMany(type => type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(property => property.Name.Contains("Space", StringComparison.Ordinal))
            .Select(property => $"{type.Name}.{property.Name}")),
    ];

    /// <summary>
    /// A planted request with a Space member, for the scan's oracle. Never used.
    /// </summary>
    /// <param name="TargetSpaceId">The member the scan must report.</param>
    /// <param name="Title">A member it must not.</param>
    internal sealed record PlantedSpaceRequest(Guid TargetSpaceId, string Title);
}
