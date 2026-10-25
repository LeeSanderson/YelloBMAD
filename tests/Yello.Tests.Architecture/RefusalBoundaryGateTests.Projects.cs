using System.Reflection;
using System.Runtime.CompilerServices;
using Xunit;
using Yello.Domain;
using Yello.Domain.Bounds;
using Yello.Domain.Memberships;

namespace Yello.Tests.Architecture;

/// <summary>
/// Story 2.1's structural gates: the creation bound and idempotency are the pipeline's (AC10, A-12, AR-3),
/// no route or contract moves a Project (AC7), and the client's Role predicate for Project controls agrees
/// with the capability matrix (AC13, Q12 (a)).
/// </summary>
/// <remarks>
/// <para>
/// <b>Written as red-phase scaffolds by the story 2.1 ATDD run on 2026-10-25.</b> The types these gates
/// are about do not exist yet, so each reaches its subject <b>by name</b> - the names the story calls
/// indicative (<c>IBoundCounter</c>, <c>IIdempotencyStore</c>, <c>BoundCounter</c>,
/// <c>IdempotencyStore</c>, a client predicate named for <c>ManageProjects</c>). On activation, replace
/// each name with a typed reference to the type dev-story actually wrote, correct the permitted lists to
/// the real adapter and registration names, and record any rename in the Debug Log.
/// </para>
/// <para>
/// <b>One gate is green today and is left active</b>:
/// <see cref="Nothing_but_the_pipeline_reads_the_bound_registry"/>. Only <c>ActiveSpaceResolution</c>
/// reads <c>BoundRegistry</c> now, and the story requires that to stay true when the creation bound
/// arrives. It was planted by the ATDD run; the result is in the checklist.
/// </para>
/// </remarks>
public sealed partial class RefusalBoundaryGateTests
{
    private const string BoundCountPortName = "Yello.Application.Spaces.IBoundCounter";
    private const string IdempotencyPortName = "Yello.Application.Spaces.IIdempotencyStore";
    private const string ProjectTypeName = "Yello.Domain.Projects.Project";
    private const string ProjectContractsNamespace = "Yello.Contracts.Projects";

    // ProjectRoutes.List / Create and ProjectRoutes.Rename, as story 2.1 Task 4 names them. This suite does
    // not reference Yello.Tests.Shared (and its ProjectRouteScaffold); on activation, read the contract.
    private const string ProjectCollectionRoute = "/api/v1/spaces/{spaceId:guid}/projects";
    private const string ProjectItemRoute = "/api/v1/spaces/{spaceId:guid}/projects/{projectId:guid}";

    /// <summary>
    /// AC10, AR-30, <c>deferred-work.md:319</c> - nothing but the pipeline reads <c>BoundRegistry</c>: a
    /// slice that read a bound's limit would be the place it is checked outside the pipeline. Standing gate,
    /// green before story 2.1.
    /// </summary>
    [Fact]
    [Trait("Priority", "P1")]
    [Trait("Requirement", "AR-3")]
    [Trait("Requirement", "AD-25")]
    public void Nothing_but_the_pipeline_reads_the_bound_registry()
    {
        var registry = typeof(BoundRegistry).FullName!;
        var dependents = DependentsOf(registry);

        // Vacuity guard: the pipeline reads it today (the presence bound), so the scan sees references.
        Assert.Contains(PipelineTypeName, dependents);

        string[] permitted = [registry, PipelineTypeName];

        var offenders = dependents.Where(name => !permitted.Contains(name, StringComparer.Ordinal)).ToList();

        Assert.True(
            offenders.Count == 0,
            $"{string.Join(", ", offenders)} read BoundRegistry. A bound is declared once and checked by the pipeline (AR-3, AD-25).");
    }

    /// <summary>
    /// AC10, A-12 - nothing but the pipeline, the port's adapter and its registration depends on the
    /// bound-count port, so the creation bound cannot be counted inside a slice. Plant for Task 12: a
    /// handler injecting the port is caught.
    /// </summary>
    [Fact(Skip = "red: AC10 - the bound-count port does not exist until story 2.1 Task 6")]
    [Trait("Priority", "P1")]
    [Trait("Requirement", "AR-3")]
    [Trait("Requirement", "AD-25")]
    public void Only_the_pipeline_depends_on_the_bound_count_port() =>
        AssertOnlyThePipelineDependsOn(
            BoundCountPortName,
            "Yello.Infrastructure.Persistence.BoundCounter",
            "Yello.Infrastructure.InfrastructureServices");

    /// <summary>
    /// AC9, A-12's idempotency clause, <c>deferred-work.md:253-256</c> - nothing but the pipeline, the
    /// store's adapter and its registration depends on the idempotency port, so no slice reads or writes
    /// an <c>Idempotency-Key</c> record. Plant for Task 12: a handler injecting the port is caught.
    /// </summary>
    [Fact(Skip = "red: AC9 - the idempotency port does not exist until story 2.1 Task 7")]
    [Trait("Priority", "P1")]
    [Trait("Requirement", "AR-25")]
    [Trait("Requirement", "AD-18")]
    public void Only_the_pipeline_depends_on_the_idempotency_port() =>
        AssertOnlyThePipelineDependsOn(
            IdempotencyPortName,
            "Yello.Infrastructure.Persistence.IdempotencyStore",
            "Yello.Infrastructure.InfrastructureServices");

    /// <summary>
    /// AC7 - every route the Host maps under a Space's <c>/projects</c> is the collection (<c>GET</c>,
    /// <c>POST</c>) or one Project (<c>PATCH</c>, the rename): no route carries a segment after the
    /// Project's id, so none can move a Project. Story 2.5 adds <c>DELETE</c> on the Project and widens the
    /// permitted set by exactly that.
    /// </summary>
    [Fact(Skip = "red: AC7 - the Project routes do not exist until story 2.1 Task 5")]
    [Trait("Requirement", "FR-17")]
    [Trait("Requirement", "AD-2")]
    public void No_route_moves_a_Project()
    {
        var projectRoutes = RouteTable()
            .Where(endpoint => endpoint.RoutePattern.RawText?.StartsWith(ProjectCollectionRoute, StringComparison.Ordinal) == true)
            .SelectMany(endpoint => (endpoint.Metadata.GetMetadata<Microsoft.AspNetCore.Routing.HttpMethodMetadata>()?.HttpMethods ?? [])
                .Select(method => $"{method} {endpoint.RoutePattern.RawText}"))
            .Order(StringComparer.Ordinal)
            .ToList();

        // Vacuity guard: the routes exist, or there is nothing to have checked.
        Assert.NotEmpty(projectRoutes);

        Assert.Equal(
            [
                $"GET {ProjectCollectionRoute}",
                $"PATCH {ProjectItemRoute}",
                $"POST {ProjectCollectionRoute}",
            ],
            projectRoutes);
    }

    /// <summary>
    /// AC7 - no Project request contract carries a Space or anything but the name: the Space is the
    /// route's, fixed at creation, and a body that could name another one is the leak closed by
    /// construction rather than by rule.
    /// </summary>
    [Fact(Skip = "red: AC7 - Yello.Contracts.Projects does not exist until story 2.1 Task 4")]
    [Trait("Requirement", "FR-17")]
    public void No_Project_request_contract_carries_anything_but_a_name()
    {
        var requests = ProductionAssemblies.Contracts.GetTypes()
            .Where(type => type.Namespace == ProjectContractsNamespace && type.Name.EndsWith("Request", StringComparison.Ordinal))
            .ToList();

        // Vacuity guard: create and rename both exist.
        Assert.Equal(["CreateProjectRequest", "RenameProjectRequest"], requests.Select(type => type.Name).Order(StringComparer.Ordinal));

        Assert.All(requests, request => Assert.Equal(
            ["Name"],
            request.GetProperties(BindingFlags.Public | BindingFlags.Instance).Select(property => property.Name)));
    }

    /// <summary>
    /// AC7, Task 11 - a Project's Space is fixed at creation in the type: <c>Project.SpaceId</c> has an
    /// <c>init</c> accessor and no ordinary setter, so no code path can assign it after construction.
    /// </summary>
    [Fact(Skip = "red: AC7 - Yello.Domain.Projects.Project does not exist until story 2.1 Task 3")]
    [Trait("Requirement", "FR-17")]
    public void A_Projects_Space_can_only_be_set_when_it_is_created()
    {
        var project = ProductionAssemblies.Domain.GetType(ProjectTypeName);

        Assert.NotNull(project);

        var setter = project.GetProperty("SpaceId", BindingFlags.Public | BindingFlags.Instance)?.SetMethod;

        Assert.NotNull(setter);
        Assert.Contains(typeof(IsExternalInit), setter.ReturnParameter.GetRequiredCustomModifiers());
    }

    /// <summary>
    /// AC13, UX-DR27, Q12 (a) - the client's predicate for showing create and rename agrees with the
    /// capability matrix for every Role: it shows them exactly where <c>ManageProjects</c> is not denied.
    /// The server still refuses; this gate keeps the client from offering what will be refused, or hiding
    /// what will not.
    /// </summary>
    [Fact(Skip = "red: AC13 - the client's ManageProjects predicate does not exist until story 2.1 Task 10 (Q12)")]
    [Trait("Requirement", "UX-DR27")]
    [Trait("Requirement", "AD-1")]
    public void The_clients_Project_controls_predicate_agrees_with_the_capability_matrix()
    {
        var predicate = ProductionAssemblies.Client.GetTypes()
            .SelectMany(type => type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static))
            .SingleOrDefault(method => method.Name.Contains("ManageProjects", StringComparison.Ordinal)
                && method.ReturnType == typeof(bool)
                && method.GetParameters() is [{ ParameterType: var only }]
                && only == typeof(string));

        Assert.True(predicate is not null, "No static bool (string roleCode) method named for ManageProjects in Yello.Client.");

        foreach (var role in Enum.GetValues<Role>())
        {
            var expected = RoleCapabilityMatrix.Decide(role, Capability.ManageProjects) != CapabilityDecision.Denied;

            Assert.True(
                predicate.Invoke(null, [StableCode.Of(role)]) is bool shown && shown == expected,
                $"The client's predicate disagrees with the matrix for {role}.");
        }
    }

    private static void AssertOnlyThePipelineDependsOn(string port, params string[] adapters)
    {
        var dependents = DependentsOf(port);

        // Vacuity guard: the pipeline is found using the port, so the port exists and the scan reads it.
        Assert.Contains(PipelineTypeName, dependents);

        string[] permitted = [port, PipelineTypeName, .. adapters];

        var offenders = dependents.Where(name => !permitted.Contains(name, StringComparer.Ordinal)).ToList();

        Assert.True(
            offenders.Count == 0,
            $"{string.Join(", ", offenders)} depend on {port}. Idempotency and bound checks are the pipeline's (AR-3); a slice that re-implements one is a defect.");
    }
}
