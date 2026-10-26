using System.Reflection;
using Microsoft.AspNetCore.Routing;
using Xunit;
using Yello.Domain.Memberships;
using Yello.Host.Spaces;

namespace Yello.Tests.Architecture;

/// <summary>
/// Story 2.5's structural gates: the two <c>DELETE</c> routes declare the capability the matrix gives deletion, serve
/// both surfaces and consult no bound (AC7, AD-4, AR-30); the Project's item route gains <c>DELETE</c> and nothing else
/// (AC7, FR-17); <c>ProjectRoutes</c> names the item template once (Task 4); the Domain ports return a typed outcome
/// (Task 2); and each deletion is one slice with one handler (AR-3). Written red by the story 2.5 ATDD run.
/// </summary>
/// <remarks>
/// <para>
/// <b>The subjects are reached by name</b> (<c>Yello.Domain.Tasks.TaskDeletion</c>, <c>ProjectRoutes.Project</c>, the
/// handlers), because none exists yet; the dev phase may switch each to a typed reference once it does, so a rename breaks
/// the build rather than emptying the gate. The route patterns stay literals: they are what the gate asserts.
/// </para>
/// <para>
/// <b>Three active gates go red by design when Task 5 maps the routes</b>, and are amended in that change by exactly the
/// <c>DELETE</c> added, never loosened (story Task 5): <c>No_route_moves_a_Project</c> (ARCH-502 below is its successor),
/// <c>The_routes_under_a_Project_are_its_rename_its_Task_creation_and_its_Board</c> (ARCH-503) and
/// <c>The_Task_routes_declare_their_capability_serve_both_surfaces_and_consult_no_bound</c> (ARCH-501 holds the
/// <c>DELETE</c>; its own list becomes <c>["DELETE", "GET", "PATCH"]</c>). The generic gates -
/// <c>Every_spaceId_endpoint_declares_exactly_one_capability</c>, the replayable-result gate, the
/// <c>Results.Created</c>/<c>Accepted</c> ban, the Space-scoped-entity gate and the pipeline-only gates - must stay green
/// unchanged.
/// </para>
/// </remarks>
public sealed partial class RefusalBoundaryGateTests
{
    private const string TaskItemPattern = "/api/v1/spaces/{spaceId:guid}/tasks/{taskId:guid}";

    private const string ProjectsPattern = "/api/v1/spaces/{spaceId:guid}/projects";

    private const string ProjectPattern = "/api/v1/spaces/{spaceId:guid}/projects/{projectId:guid}";

    private static readonly string[] DeletionOutcomes = ["Deleted", "NotFound"];

    /// <summary>
    /// ARCH-501 - AC7, AD-4, AD-1, AR-30, Task 5 - <c>DELETE</c> is mapped on the Task's item route requiring
    /// <c>ManageTasks</c> and on the Project's item route requiring <c>ManageProjects</c> - the matrix's own delete
    /// capabilities (<c>prd.md:341-342</c>; no new capability) - neither is Session-only, and neither consults a bound: a
    /// deletion adds nothing, so counting would refuse deletions in a full Project or Space.
    /// </summary>
    [Fact]
    [Trait("Requirement", "AD-4")]
    [Trait("Requirement", "AD-1")]
    [Trait("Requirement", "FR-23")]
    public void The_delete_routes_declare_their_capability_serve_both_surfaces_and_consult_no_bound()
    {
        var deletes = RouteTable()
            .Where(endpoint => endpoint.RoutePattern.RawText is TaskItemPattern or ProjectPattern)
            .Where(endpoint => endpoint.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods.Contains("DELETE") == true)
            .ToDictionary(endpoint => endpoint.RoutePattern.RawText!, StringComparer.Ordinal);

        Assert.Equal([ProjectPattern, TaskItemPattern], deletes.Keys.Order(StringComparer.Ordinal));
        Assert.Equal(Capability.ManageTasks, deletes[TaskItemPattern].Metadata.GetMetadata<RequiredCapability>()?.Capability);
        Assert.Equal(Capability.ManageProjects, deletes[ProjectPattern].Metadata.GetMetadata<RequiredCapability>()?.Capability);
        Assert.All(deletes.Values, endpoint => Assert.DoesNotContain(endpoint.Metadata, metadata => metadata.GetType().Name == "SessionOnlyEndpoint"));
        Assert.All(deletes.Values, endpoint => Assert.Null(endpoint.Metadata.GetMetadata<RequiredBound>()));
    }

    /// <summary>
    /// ARCH-502 - AC7, FR-17, AD-2 - the successor of 2.1's <c>No_route_moves_a_Project</c>, widened by exactly what its
    /// own remarks promised: the Project collection is <c>GET</c> and <c>POST</c>, the item is <c>PATCH</c> (rename) and
    /// <c>DELETE</c>, and nothing else - still no method that moves a Project. It replaced 2.1's gate when story 2.5
    /// mapped the deletion; the collection and the item are matched exactly, and ARCH-503 holds what lies under the item,
    /// so a route that moved a Project through a new segment is still caught.
    /// </summary>
    [Fact]
    [Trait("Requirement", "FR-17")]
    [Trait("Requirement", "AD-2")]
    public void The_Project_routes_are_list_create_rename_and_delete_and_nothing_moves_a_Project()
    {
        var routes = RouteTable()
            .Where(endpoint => endpoint.RoutePattern.RawText is ProjectsPattern or ProjectPattern)
            .SelectMany(endpoint => (endpoint.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods ?? [])
                .Select(method => $"{method} {endpoint.RoutePattern.RawText}"))
            .Order(StringComparer.Ordinal)
            .ToList();

        Assert.Equal(
            [
                $"DELETE {ProjectPattern}",
                $"GET {ProjectsPattern}",
                $"PATCH {ProjectPattern}",
                $"POST {ProjectsPattern}",
            ],
            routes);
    }

    /// <summary>
    /// ARCH-503 - AC7, FR-19, FR-28 - the successor of 2.2's
    /// <c>The_routes_under_a_Project_are_its_rename_its_Task_creation_and_its_Board</c>: under one Project the Host maps
    /// its deletion, its rename, its Task creation and its Board, and nothing that lists its Tasks flat (2.2's AC5 total
    /// would then be the client's to derive) and nothing that moves a Project. It replaced 2.2's gate when story 2.5
    /// mapped the deletion.
    /// </summary>
    [Fact]
    [Trait("Requirement", "FR-19")]
    [Trait("Requirement", "FR-28")]
    public void The_routes_under_a_Project_are_its_deletion_its_rename_its_Task_creation_and_its_Board()
    {
        var routes = RouteTable()
            .Where(endpoint => endpoint.RoutePattern.RawText?.StartsWith(ProjectPattern, StringComparison.Ordinal) == true)
            .SelectMany(endpoint => (endpoint.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods ?? [])
                .Select(method => $"{method} {endpoint.RoutePattern.RawText}"))
            .Order(StringComparer.Ordinal)
            .ToList();

        Assert.Equal(
            [
                $"DELETE {ProjectPattern}",
                $"GET {ProjectPattern}/board",
                $"PATCH {ProjectPattern}",
                $"POST {ProjectPattern}/tasks",
            ],
            routes);
    }

    /// <summary>
    /// ARCH-504 - Task 4 - <c>ProjectRoutes</c> names the Project's item template once: <c>Project</c> is the pattern,
    /// and <c>Rename</c> is the same template (two names, one route), so the route table and the isolation rows compare
    /// one string.
    /// </summary>
    [Fact]
    [Trait("Requirement", "AR-35")]
    public void ProjectRoutes_names_the_Project_item_template_once()
    {
        var routes = typeof(Yello.Contracts.Projects.ProjectRoutes);
        var project = routes.GetProperty("Project", BindingFlags.Public | BindingFlags.Static);

        Assert.True(project is not null, "ProjectRoutes has no static Project property.");
        Assert.Equal(ProjectPattern, project.GetValue(null));
        Assert.Equal(ProjectPattern, Yello.Contracts.Projects.ProjectRoutes.Rename);
    }

    /// <summary>
    /// ARCH-505 - AC7, Task 2 - the Domain ports answer a deletion with a typed outcome, as <c>ILabelStore.DeleteAsync</c>
    /// does: <c>ITaskStore.DeleteAsync(Guid, CancellationToken)</c> returns <c>Task&lt;TaskDeletion&gt;</c> and
    /// <c>IProjectStore.DeleteAsync(Guid, CancellationToken)</c> returns <c>Task&lt;ProjectDeletion&gt;</c>, each enum
    /// exactly <c>{Deleted, NotFound}</c> - no "already deleted", which would tell a caller an id once existed (Q4).
    /// </summary>
    [Fact]
    [Trait("Requirement", "AD-3")]
    [Trait("Requirement", "FR-23")]
    public void The_Task_and_Project_stores_answer_a_deletion_with_Deleted_or_NotFound()
    {
        foreach (var (port, outcome) in new[]
        {
            ("Yello.Domain.Tasks.ITaskStore", "Yello.Domain.Tasks.TaskDeletion"),
            ("Yello.Domain.Projects.IProjectStore", "Yello.Domain.Projects.ProjectDeletion"),
        })
        {
            var portType = ProductionAssemblies.Domain.GetType(port);
            var outcomeType = ProductionAssemblies.Domain.GetType(outcome);

            Assert.True(portType is not null, $"{port} does not exist.");
            Assert.True(outcomeType is not null, $"{outcome} does not exist.");
            Assert.True(outcomeType.IsEnum, $"{outcome} is not an enum.");
            Assert.Equal(DeletionOutcomes, Enum.GetNames(outcomeType).Order(StringComparer.Ordinal));

            var delete = portType.GetMethod("DeleteAsync", [typeof(Guid), typeof(CancellationToken)]);

            Assert.True(delete is not null, $"{port} has no DeleteAsync(Guid, CancellationToken).");
            Assert.Equal(typeof(Task<>).MakeGenericType(outcomeType), delete.ReturnType);
        }
    }

    /// <summary>
    /// ARCH-506 - AR-3, Task 2 - each deletion is one slice with one handler, the <c>DeleteLabelHandler</c> shape:
    /// <c>Yello.Application.Tasks.DeleteTask.DeleteTaskHandler</c> and
    /// <c>Yello.Application.Projects.DeleteProject.DeleteProjectHandler</c>, each a sealed class with a
    /// <c>HandleAsync(Guid, CancellationToken)</c>. What a handler must not read (the Role, a capability, a bound count,
    /// the <c>Idempotency-Key</c>) is the existing pipeline-only gates' to hold; this gate only makes sure they have
    /// something to hold.
    /// </summary>
    [Fact]
    [Trait("Requirement", "AR-3")]
    public void Each_deletion_is_one_slice_with_one_handler()
    {
        foreach (var name in new[] { "Yello.Application.Tasks.DeleteTask.DeleteTaskHandler", "Yello.Application.Projects.DeleteProject.DeleteProjectHandler" })
        {
            var handler = ProductionAssemblies.Application.GetType(name);

            Assert.True(handler is not null, $"{name} does not exist.");
            Assert.True(handler.IsSealed, $"{name} is not sealed.");
            Assert.NotNull(handler.GetMethod("HandleAsync", [typeof(Guid), typeof(CancellationToken)]));
        }
    }
}
