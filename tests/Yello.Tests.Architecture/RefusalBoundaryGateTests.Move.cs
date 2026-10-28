using System.Reflection;
using Microsoft.AspNetCore.Routing;
using Xunit;
using Yello.Domain.Memberships;
using Yello.Host.Spaces;

namespace Yello.Tests.Architecture;

/// <summary>
/// Story 2.6's structural gates: the move route requires <c>ManageTasks</c>, is Session-only and consults no bound (AC8,
/// AC9, Q1 (a), Q17, FR-35, AD-4, AR-30); the Token-unreachable set widens by exactly that route; the routes under a Task are
/// its item methods and its move and nothing else; the contract names the route once and its request carries no key; the
/// Q3 problem type exists; and the move is one Domain port and one slice (AR-3). Written red by the story 2.6 ATDD run.
/// </summary>
/// <remarks>
/// <para>
/// <b>The subjects are reached by name</b> (<c>TaskRoutes.Move</c>, <c>MoveTaskRequest</c>, <c>ITaskStore.MoveAsync</c>,
/// <c>MoveTaskHandler</c>, <c>ProblemTypes.TaskPositionUnavailable</c>), as the ATDD run wrote them. The route patterns
/// stay literals: they are what the gate asserts.
/// </para>
/// <para>
/// <b>Gates the move widened, by exactly the move</b> (story 2.6 Task 6): 1.8's Token-unreachable gate is replaced by
/// ARCH-602, which keeps its vacuity guard and adds the move with FR-35's reason; the two exact sets of Task request types
/// (<c>RefusalBoundaryGateTests.Tasks.cs</c>, <c>.TaskEdit.cs</c>) gained <c>MoveTaskRequest</c>. Must stay green
/// unchanged: <c>RefusalBoundaryGateTests.Delete.cs</c>'s ARCH-501 (the deletes are not Session-only),
/// <c>The_Task_edit_request_carries_its_editable_attributes_and_nothing_else</c>, the response-shape pins
/// (<c>TaskProperties</c>), <c>Every_spaceId_endpoint_declares_exactly_one_capability</c>, the replayable-result gate, the
/// <c>Results.Created</c>/<c>Accepted</c> ban and the Space-scoped-entity gate.
/// </para>
/// </remarks>
public sealed partial class RefusalBoundaryGateTests
{
    private const string TaskMovePattern = "/api/v1/spaces/{spaceId:guid}/tasks/{taskId:guid}/move";

    /// <summary>
    /// ARCH-601 - AC8, AC9, Q1 (a), Q17, FR-35, AD-4, AR-30 - <c>POST</c> is mapped on the move route requiring
    /// <c>ManageTasks</c> (the matrix's "Create, edit, delete Tasks", <c>prd.md:342</c>; no new capability), marked
    /// Session-only - endpoint metadata, not a slice that branches on the surface - and consulting no bound: a move creates
    /// nothing, so counting would refuse moves in a full Project (2.3 P3).
    /// </summary>
    [Fact]
    [Trait("Requirement", "FR-35")]
    [Trait("Requirement", "FR-29")]
    [Trait("Requirement", "AD-4")]
    public void The_move_route_requires_ManageTasks_is_Session_only_and_consults_no_bound()
    {
        var move = Assert.Single(RouteTable(), endpoint => endpoint.RoutePattern.RawText == TaskMovePattern);

        Assert.Equal(["POST"], move.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods ?? []);
        Assert.Equal(Capability.ManageTasks, move.Metadata.GetMetadata<RequiredCapability>()?.Capability);
        Assert.Contains(move.Metadata, metadata => metadata.GetType().FullName == SessionOnlyMarkerName);
        Assert.Null(move.Metadata.GetMetadata<RequiredBound>());
        Assert.True(CarriesSpaceId(move), "The move route does not carry {spaceId}.");
    }

    /// <summary>
    /// ARCH-602 - AC8, FR-35, FR-36, AR-29, AD-24 - the successor of 1.8's Token-unreachable gate, widened by exactly the
    /// move route: the endpoints marked unreachable with an API Token, by the Session-only marker or by an AD-24 surface,
    /// are Token issue, the Token list, Token revocation, the switcher - so a new Token-management route cannot be added
    /// without one - and the move: the one place the API is deliberately narrower than the browser (FR-35,
    /// <c>prd.md:605</c>), because a position within a column is readable over the API and never writable.
    /// </summary>
    /// <remarks>
    /// A Token authenticates only on a Space-scoped route without <c>SessionOnlyEndpoint</c>; every other route answers it
    /// as no credential by its shape. The two markers name the exceptions a shape cannot decide. Read from the real route
    /// table; <b>vacuity guard:</b> the issue route and the move route are found carrying <c>{spaceId}</c>, so the marker
    /// is doing work their shape would not.
    /// </remarks>
    [Fact]
    [Trait("Requirement", "FR-35")]
    [Trait("Requirement", "FR-36")]
    [Trait("Requirement", "AR-29")]
    [Trait("Requirement", "AD-24")]
    public void The_endpoints_unreachable_with_a_Token_are_Token_management_the_switcher_and_the_move()
    {
        var table = RouteTable();

        var issue = Assert.Single(table, endpoint => endpoint.RoutePattern.RawText == Yello.Contracts.ApiTokens.ApiTokenRoutes.Issue);
        Assert.True(CarriesSpaceId(issue), "The issue route no longer carries {spaceId}.");

        var move = Assert.Single(table, endpoint => endpoint.RoutePattern.RawText == TaskMovePattern);
        Assert.True(CarriesSpaceId(move), "The move route no longer carries {spaceId}.");

        var marked = table
            .Where(endpoint => endpoint.Metadata.Any(item =>
                item.GetType().FullName is SessionOnlyMarkerName or AccountScopedMarkerName))
            .Select(endpoint => endpoint.RoutePattern.RawText)
            .Order(StringComparer.Ordinal)
            .ToList();

        Assert.Equal(
            new[]
            {
                Yello.Contracts.ApiTokens.ApiTokenRoutes.List,
                Yello.Contracts.ApiTokens.ApiTokenRoutes.Revoke,
                Yello.Contracts.Spaces.SpaceRoutes.Directory,
                Yello.Contracts.ApiTokens.ApiTokenRoutes.Issue,
                TaskMovePattern,
            }.Order(StringComparer.Ordinal),
            marked);
    }

    /// <summary>
    /// ARCH-603 - AC9, FR-29, FR-41's boundary - under one Task the Host maps its read, edit and deletion on the item and
    /// its move, and nothing else: no route that moves a Task to another Project (6.5) or writes a position key directly.
    /// </summary>
    [Fact]
    [Trait("Requirement", "FR-29")]
    [Trait("Requirement", "FR-35")]
    public void The_routes_under_a_Task_are_its_read_edit_deletion_and_move()
    {
        var routes = RouteTable()
            .Where(endpoint => endpoint.RoutePattern.RawText?.StartsWith(TaskItemPattern, StringComparison.Ordinal) == true)
            .SelectMany(endpoint => (endpoint.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods ?? [])
                .Select(method => $"{method} {endpoint.RoutePattern.RawText}"))
            .Order(StringComparer.Ordinal)
            .ToList();

        Assert.Equal(
            [
                $"DELETE {TaskItemPattern}",
                $"GET {TaskItemPattern}",
                $"PATCH {TaskItemPattern}",
                $"POST {TaskMovePattern}",
            ],
            routes);
    }

    /// <summary>
    /// ARCH-604 - AC9, Q1 (a), AR-35 - <c>TaskRoutes</c> names the move template once (<c>Move</c>), and <c>MoveOf</c> builds
    /// that path for a Space and a Task, so the route table, the client and the isolation rows compare one string.
    /// </summary>
    [Fact]
    [Trait("Requirement", "AR-35")]
    public void TaskRoutes_names_the_move_template_once()
    {
        var routes = typeof(Yello.Contracts.Tasks.TaskRoutes);
        var move = routes.GetProperty("Move", BindingFlags.Public | BindingFlags.Static);
        var moveOf = routes.GetMethod("MoveOf", BindingFlags.Public | BindingFlags.Static, [typeof(Guid), typeof(Guid)]);

        Assert.True(move is not null, "TaskRoutes has no static Move property.");
        Assert.True(moveOf is not null, "TaskRoutes has no static MoveOf(Guid, Guid).");
        Assert.Equal(TaskMovePattern, move.GetValue(null));

        var (spaceId, taskId) = (Guid.NewGuid(), Guid.NewGuid());

        Assert.Equal(Yello.Contracts.Tasks.TaskRoutes.TaskOf(spaceId, taskId) + "/move", moveOf.Invoke(null, [spaceId, taskId]));
    }

    /// <summary>
    /// ARCH-605 - AC8, AC9, Q2 (a), FR-35, AR-21 - the move request names a destination Status and a neighbour, and nothing
    /// else: <c>MoveTaskRequest</c>'s public properties are exactly <c>AfterTaskId</c> (<see cref="Nullable{T}"/> of
    /// <see cref="Guid"/>) and <c>StatusId</c> (<see cref="Guid"/>) - no position key, no Project, no Space.
    /// </summary>
    [Fact]
    [Trait("Requirement", "FR-35")]
    [Trait("Requirement", "AR-21")]
    public void The_move_request_names_a_Status_and_a_neighbour_and_never_a_key()
    {
        var request = typeof(Yello.Contracts.Tasks.TaskRoutes).Assembly.GetType("Yello.Contracts.Tasks.MoveTaskRequest");

        Assert.True(request is not null, "Yello.Contracts.Tasks.MoveTaskRequest does not exist.");

        var properties = request
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(property => $"{property.Name}:{property.PropertyType.Name}")
            .Order(StringComparer.Ordinal)
            .ToList();

        Assert.Equal(["AfterTaskId:Nullable`1", "StatusId:Guid"], properties);
    }

    /// <summary>
    /// ARCH-606 - AC9, Q3 (a), Q10 - the problem type a neighbour that is not one (and an exhausted gap) answers is stated
    /// once in the contract: <c>ProblemTypes.TaskPositionUnavailable</c> is
    /// <c>urn:yello:problem:task-position-unavailable</c>.
    /// </summary>
    [Fact]
    [Trait("Requirement", "AR-34")]
    public void The_position_refusal_is_a_stable_problem_type()
    {
        var type = typeof(Yello.Contracts.ProblemTypes).GetProperty("TaskPositionUnavailable", BindingFlags.Public | BindingFlags.Static);

        Assert.True(type is not null, "ProblemTypes has no static TaskPositionUnavailable.");
        Assert.Equal("urn:yello:problem:task-position-unavailable", type.GetValue(null));
    }

    /// <summary>
    /// ARCH-607 - AC3, AC9, story Task 3, AR-3 - a move is one Domain port and one slice: <c>ITaskStore</c> declares a
    /// <c>MoveAsync</c> taking the Task, the destination Status, the neighbour (<c>Guid?</c>) and a
    /// <see cref="CancellationToken"/> - no Space parameter - and returning a typed outcome
    /// (<c>Task&lt;TaskMove…&gt;</c>, the <c>TaskDeletion</c> shape); and
    /// <c>Yello.Application.Tasks.MoveTask.MoveTaskHandler</c> is a sealed class with a <c>HandleAsync</c>. What a handler
    /// must not read (the Role, a capability, a bound, the <c>Idempotency-Key</c>) is the existing pipeline-only gates' to
    /// hold.
    /// </summary>
    [Fact]
    [Trait("Requirement", "AR-3")]
    [Trait("Requirement", "AD-2")]
    public void A_move_is_one_Domain_port_and_one_slice()
    {
        var port = ProductionAssemblies.Domain.GetType("Yello.Domain.Tasks.ITaskStore");

        Assert.True(port is not null, "Yello.Domain.Tasks.ITaskStore does not exist.");

        var move = port.GetMethod("MoveAsync", [typeof(Guid), typeof(Guid), typeof(Guid?), typeof(CancellationToken)]);

        Assert.True(move is not null, "ITaskStore has no MoveAsync(Guid, Guid, Guid?, CancellationToken).");
        Assert.True(move.ReturnType.IsGenericType && move.ReturnType.GetGenericTypeDefinition() == typeof(Task<>), "MoveAsync does not return a Task<T>.");
        Assert.StartsWith("TaskMove", move.ReturnType.GetGenericArguments()[0].Name, StringComparison.Ordinal);

        var handler = ProductionAssemblies.Application.GetType("Yello.Application.Tasks.MoveTask.MoveTaskHandler");

        Assert.True(handler is not null, "Yello.Application.Tasks.MoveTask.MoveTaskHandler does not exist.");
        Assert.True(handler.IsSealed, "MoveTaskHandler is not sealed.");
        Assert.Contains(handler.GetMethods(BindingFlags.Public | BindingFlags.Instance), method => method.Name == "HandleAsync");
    }
}
