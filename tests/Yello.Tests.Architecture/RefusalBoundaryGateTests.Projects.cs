using System.Reflection;
using System.Runtime.CompilerServices;
using Xunit;
using Yello.Domain;
using Yello.Domain.Memberships;

namespace Yello.Tests.Architecture;

/// <summary>
/// Story 2.1's structural gates: the creation bound and idempotency are the pipeline's (AC10, A-12, AR-3),
/// no route or contract moves a Project (AC7), and the client's Role predicate for Project controls agrees
/// with the capability matrix (AC13, Q12 (a)).
/// </summary>
/// <remarks>
/// <para>
/// <b>Each gate reaches its subject by a typed reference</b> (the ports, the routes, <c>Project</c>), so a
/// rename breaks the build rather than emptying the gate. The permitted lists name the ports' adapters
/// and <c>SpaceBoundaryLimits</c>, where they are registered.
/// </para>
/// <para>
/// <b>Each was planted</b> (story 2.1's Debug Log): a handler injecting either port, a client predicate
/// that shows the controls to a Viewer, and - for the bound-registry gate in <c>RefusalBoundaryGateTests.StatusBulkMove.cs</c>
/// - the ATDD run's own plant.
/// </para>
/// </remarks>
public sealed partial class RefusalBoundaryGateTests
{
    /// <summary>
    /// One of the two cross-Project slices admitted to count a bound: a move into another Project names its destination in
    /// the body, so the pipeline cannot count it, and a pipeline lock on the destination taken before the slice's sorted pair
    /// of Project locks would let two opposite moves deadlock. It counts with the pipeline's own counter and limit, after its
    /// locks.
    /// </summary>
    private const string MovesATaskToAnotherProject = "Yello.Application.Tasks.MoveTaskToProject.MoveTaskToProjectHandler";

    private static string BoundCountPortName => typeof(Yello.Application.Spaces.IBoundCounter).FullName!;
    private static string IdempotencyPortName => typeof(Yello.Application.Spaces.IIdempotencyStore).FullName!;
    private static string ProjectContractsNamespace => typeof(Yello.Contracts.Projects.ProjectRoutes).Namespace!;

    /// <summary>
    /// AC9, A-12's idempotency clause, <c>deferred-work.md:253-256</c> - nothing but the pipeline, the
    /// store's adapter and its registration depends on the idempotency port, so no slice reads or writes
    /// an <c>Idempotency-Key</c> record. Plant for Task 12: a handler injecting the port is caught.
    /// </summary>
    [Fact]
    [Trait("Priority", "P1")]
    [Trait("Requirement", "AR-25")]
    [Trait("Requirement", "AD-18")]
    public void Only_the_pipeline_depends_on_the_idempotency_port() =>
        AssertOnlyThePipelineDependsOn(
            IdempotencyPortName,
            "Yello.Infrastructure.Persistence.IdempotencyStore",
            "Yello.Infrastructure.SpaceBoundaryLimits");

    /// <summary>
    /// Story 3.1, Q2 (a), A-12's idempotency clause - nothing but the pipeline, the store's adapter and its registration
    /// depends on the Account-scoped idempotency port Space creation replays from.
    /// </summary>
    [Fact]
    [Trait("Priority", "P1")]
    [Trait("Requirement", "AR-25")]
    [Trait("Requirement", "AD-18")]
    public void Only_the_pipeline_depends_on_the_Account_scoped_idempotency_port() =>
        AssertOnlyThePipelineDependsOn(
            typeof(Yello.Application.Spaces.IAccountIdempotencyStore).FullName!,
            "Yello.Infrastructure.Persistence.AccountIdempotencyStore",
            "Yello.Infrastructure.SpaceBoundaryLimits");

    /// <summary>
    /// Story 3.1, Q1 (a), AR-3 - nothing but the pipeline, the unit of work's adapter and its registration depends on the
    /// Space creation scope: no slice opens the one unit of work that carries both session keys.
    /// </summary>
    [Fact]
    [Trait("Priority", "P0")]
    [Trait("Requirement", "AR-3")]
    [Trait("Requirement", "AD-24")]
    public void Only_the_pipeline_opens_the_Space_creation_unit_of_work() =>
        AssertOnlyThePipelineDependsOn(
            typeof(Yello.Application.Spaces.ISpaceCreationScope).FullName!,
            "Yello.Infrastructure.Persistence.SpaceCreationScope",
            "Yello.Infrastructure.AccountScopedReads");

    /// <summary>
    /// AC7 - no Project request contract carries a Space or anything but the name: the Space is the
    /// route's, fixed at creation, and a body that could name another one is the leak closed by
    /// construction rather than by rule.
    /// </summary>
    [Fact]
    [Trait("Requirement", "FR-17")]
    public void No_Project_request_contract_carries_anything_but_a_name()
    {
        var requests = ProductionAssemblies.Contracts.GetTypes()
            .Where(type => type.Namespace == ProjectContractsNamespace && type.Name.EndsWith("Request", StringComparison.Ordinal))
            .ToList();

        // Vacuity guard: create and rename both exist, and so do the Project's own Status requests, which share the
        // namespace and are held to their members by the Project Status gates.
        Assert.Equal(
            ["AddProjectStatusRequest", "CreateProjectRequest", "PlaceProjectStatusRequest", "RemoveProjectStatusRequest", "RenameProjectRequest", "RenameProjectStatusRequest"],
            requests.Select(type => type.Name).Order(StringComparer.Ordinal));

        Assert.All(requests.Where(request => !request.Name.Contains("Status", StringComparison.Ordinal)), request => Assert.Equal(
            ["Name"],
            request.GetProperties(BindingFlags.Public | BindingFlags.Instance).Select(property => property.Name)));
        Assert.All(requests, request => Assert.DoesNotContain(
            request.GetProperties(BindingFlags.Public | BindingFlags.Instance),
            property => property.Name.Contains("Space", StringComparison.Ordinal) || property.Name == "ProjectId"));
    }

    /// <summary>
    /// AC7, Task 11 - a Project's Space is fixed at creation in the type: <c>Project.SpaceId</c> has an
    /// <c>init</c> accessor and no ordinary setter, so no code path can assign it after construction.
    /// </summary>
    [Fact]
    [Trait("Requirement", "FR-17")]
    public void A_Projects_Space_can_only_be_set_when_it_is_created()
    {
        var setter = typeof(Yello.Domain.Projects.Project)
            .GetProperty(nameof(Yello.Domain.Projects.Project.SpaceId), BindingFlags.Public | BindingFlags.Instance)?.SetMethod;

        Assert.NotNull(setter);
        Assert.Contains(typeof(IsExternalInit), setter.ReturnParameter.GetRequiredCustomModifiers());
    }

    /// <summary>
    /// AC7, Task 11 layer (2) - the one production type whose IL writes <c>Project.SpaceId</c> is the
    /// store's create path, which takes the Space from the pipeline's context. An <c>init</c> accessor
    /// still lets any object initialiser choose a Space; this gate holds that choice to one place.
    /// </summary>
    [Fact]
    [Trait("Requirement", "FR-17")]
    [Trait("Requirement", "AD-2")]
    public void Only_the_create_path_writes_a_Projects_Space()
    {
        var writers = RoleReaders(typeof(Yello.Domain.Projects.Project).FullName!, "set_" + nameof(Yello.Domain.Projects.Project.SpaceId));

        // Vacuity guard: the create path is found writing it, so the scan reads the setter's call sites.
        Assert.Equal(["Yello.Infrastructure.Persistence.ProjectStore"], writers);
    }

    /// <summary>
    /// AC13, UX-DR27, Q12 (a) - the client's predicate for showing create and rename agrees with the
    /// capability matrix for every Role: it shows them exactly where <c>ManageProjects</c> is not denied.
    /// The server still refuses; this gate keeps the client from offering what will be refused, or hiding
    /// what will not.
    /// </summary>
    [Fact]
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
