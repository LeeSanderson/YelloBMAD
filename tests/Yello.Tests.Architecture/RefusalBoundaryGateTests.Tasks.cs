using System.Reflection;
using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Mono.Cecil;
using Xunit;
using Yello.Domain;
using Yello.Domain.Bounds;
using Yello.Domain.Memberships;
using Yello.Host.Spaces;

namespace Yello.Tests.Architecture;

/// <summary>
/// Story 2.2's structural gates: the gates' own entity list names real types (AC8, Task 2); a Task's Space
/// is fixed at creation in the type and written in one place (AC2); the Task and Board contracts carry no
/// Space and nothing a creation could choose (AC2, AC5); the routes under a Project are exactly the ones 2.2
/// adds (AC9); the Tasks-per-Project bound is declared only where the Project is in the route (AC11); the
/// client's Task predicate agrees with the matrix (AC3); and the idempotency record cannot silently
/// mis-record a write (AC12). Written by the story 2.2 ATDD run.
/// </summary>
/// <remarks>
/// <para>
/// <b>The subjects are typed references</b> - <see cref="TaskEntityName"/> (Q1 (b), <c>TaskItem</c>), the
/// <c>Yello.Contracts.Tasks</c> and <c>Yello.Contracts.Boards</c> namespaces - so a rename breaks the build
/// rather than emptying the gate. <c>TaskStore</c> stays a name, being internal to Infrastructure.
/// </para>
/// <para>
/// <b>Two gates were green before 2.2 wrote anything</b>, because the rule they hold was already true of
/// 2.1's endpoints: <see cref="No_endpoint_answers_through_Results_Created_or_Accepted"/> and
/// <see cref="Every_Space_scoped_state_changing_endpoint_answers_a_result_the_record_can_replay"/>. Each
/// carries a permanent oracle that runs its rule over a planted subject in this assembly and must report it.
/// Every gate here was planted in story 2.2's dev phase (its Debug Log).
/// </para>
/// </remarks>
public sealed partial class RefusalBoundaryGateTests
{
    /// <summary>
    /// The Task entity's full name under Q1 (b): the CLR type is <c>TaskItem</c>, because <c>Task</c> collides
    /// with <see cref="System.Threading.Tasks.Task"/> under implicit usings; the table, namespace, routes and
    /// copy stay "Task".
    /// </summary>
    private static string TaskEntityName => typeof(Yello.Domain.Tasks.TaskItem).FullName!;

    private static string TaskContractsNamespace => typeof(Yello.Contracts.Tasks.TaskRoutes).Namespace!;

    private static string BoardContractsNamespace => typeof(Yello.Contracts.Boards.BoardRoutes).Namespace!;

    /// <summary>
    /// Every route under one Project, as story 2.2 leaves them (Q5 (a)).
    /// </summary>
    private const string ProjectItemPattern = "/api/v1/spaces/{spaceId:guid}/projects/{projectId:guid}";

    private static readonly string[] BannedResultFactories = ["Created", "CreatedAtRoute", "Accepted", "AcceptedAtRoute"];

    private static readonly string[] ResultFactoryTypes = ["Microsoft.AspNetCore.Http.Results", "Microsoft.AspNetCore.Http.TypedResults"];

    private static readonly string[] StateChangingMethods = ["POST", "PUT", "PATCH", "DELETE"];

    /// <summary>
    /// AC8, Task 2 - every name in <c>SpaceScopedEntities</c> other than <c>Label</c> (story 2.4) resolves to a
    /// type in the Domain assembly. Today a wrong name in that list silently covers nothing: it names
    /// <c>Yello.Domain.Tasks.Task</c>, which will never exist under Q1 (b). Dev-story updates the list to the
    /// real name in the same change.
    /// </summary>
    [Fact]
    [Trait("Requirement", "AD-2")]
    [Trait("Requirement", "AD-3")]
    public void Every_Space_scoped_entity_the_gates_name_resolves_to_a_Domain_type()
    {
        var listed = SpaceScopedEntities.Where(name => name != "Yello.Domain.Labels.Label").ToList();

        // Vacuity guard: the list names the Task, so the assertion below is about it.
        Assert.Contains(TaskEntityName, listed);

        Assert.Equal([], listed.Where(name => ProductionAssemblies.Domain.GetType(name) is null));
    }

    /// <summary>
    /// AC2 ("permanently to one Space"), AC8 - a Task's Space is fixed at creation in the type:
    /// <c>SpaceId</c> has an <c>init</c> accessor and no ordinary setter.
    /// </summary>
    [Fact]
    [Trait("Requirement", "FR-19")]
    [Trait("Requirement", "AD-2")]
    public void A_Tasks_Space_can_only_be_set_when_it_is_created()
    {
        var task = ProductionAssemblies.Domain.GetType(TaskEntityName);

        Assert.True(task is not null, $"No {TaskEntityName} in the Domain assembly.");

        var setter = task.GetProperty("SpaceId", BindingFlags.Public | BindingFlags.Instance)?.SetMethod;

        Assert.NotNull(setter);
        Assert.Contains(typeof(IsExternalInit), setter.ReturnParameter.GetRequiredCustomModifiers());
    }

    /// <summary>
    /// AC2, Task 12 - the one production type whose IL writes a Task's <c>SpaceId</c> is the store's create
    /// path, which takes the Space from the pipeline's context. Sibling of 2.1's
    /// <c>Only_the_create_path_writes_a_Projects_Space</c>.
    /// </summary>
    [Fact]
    [Trait("Requirement", "FR-19")]
    [Trait("Requirement", "AD-2")]
    public void Only_the_create_path_writes_a_Tasks_Space()
    {
        var writers = RoleReaders(TaskEntityName, "set_SpaceId");

        // Vacuity guard and assertion in one: the create path is found writing it, and nothing else is.
        Assert.Equal(["Yello.Infrastructure.Persistence.TaskStore"], writers);
    }

    /// <summary>
    /// AC2, Task 3 - the Task creation request carries a title and nothing else: no Status, Project, Space or
    /// position, so "takes the first Status" and "belongs to the route's Project and Space" are true by
    /// construction.
    /// </summary>
    [Fact]
    [Trait("Requirement", "FR-19")]
    [Trait("Requirement", "AD-15")]
    public void The_Task_creation_request_carries_a_title_and_nothing_else()
    {
        var requests = ProductionAssemblies.Contracts.GetTypes()
            .Where(type => type.Namespace == TaskContractsNamespace && type.Name.EndsWith("Request", StringComparison.Ordinal))
            .ToList();

        Assert.Equal(["CreateTaskRequest"], requests.Select(type => type.Name));
        Assert.Equal(["Title"], requests[0].GetProperties(BindingFlags.Public | BindingFlags.Instance).Select(property => property.Name));
    }

    /// <summary>
    /// AC5, Task 3 - no Task or Board response carries a <c>SpaceId</c>, and a column's <c>Total</c> is a
    /// property of its own beside its <c>Tasks</c>, never derived from them on the client (AD-29).
    /// </summary>
    [Fact]
    [Trait("Requirement", "AD-29")]
    [Trait("Requirement", "AD-3")]
    public void No_Task_or_Board_response_carries_a_SpaceId_and_a_column_carries_its_own_total()
    {
        var responses = ProductionAssemblies.Contracts.GetTypes()
            .Where(type => (type.Namespace == TaskContractsNamespace || type.Namespace == BoardContractsNamespace)
                && type.Name.EndsWith("Response", StringComparison.Ordinal))
            .ToList();

        // Vacuity guard: the four Task 3 names.
        Assert.Equal(
            ["BoardColumnResponse", "BoardProjectResponse", "BoardResponse", "TaskResponse"],
            responses.Select(type => type.Name).Order(StringComparer.Ordinal));

        Assert.All(responses, response => Assert.DoesNotContain(
            response.GetProperties(BindingFlags.Public | BindingFlags.Instance),
            property => property.Name.Equals("SpaceId", StringComparison.OrdinalIgnoreCase)));

        var column = responses.Single(type => type.Name == "BoardColumnResponse");

        Assert.Equal(typeof(int), column.GetProperty("Total")?.PropertyType);
        Assert.NotNull(column.GetProperty("Tasks"));
    }

    /// <summary>
    /// AC9, AC7 of 2.1, Q5 (a) - every route the Host maps under one Project is the rename (<c>PATCH</c>), the
    /// Task creation (<c>POST …/tasks</c>) or the Board (<c>GET …/board</c>): nothing lists a Project's Tasks
    /// flat (AC5's total would then be the client's to derive), and nothing moves a Project. Read beside
    /// <c>No_route_moves_a_Project</c>, whose prefix also matches these two routes: dev-story narrows that
    /// gate to the collection and the item, and this one holds what lies under the item.
    /// </summary>
    [Fact]
    [Trait("Requirement", "FR-19")]
    [Trait("Requirement", "FR-28")]
    public void The_routes_under_a_Project_are_its_rename_its_Task_creation_and_its_Board()
    {
        var routes = RouteTable()
            .Where(endpoint => endpoint.RoutePattern.RawText?.StartsWith(ProjectItemPattern, StringComparison.Ordinal) == true)
            .SelectMany(endpoint => (endpoint.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods ?? [])
                .Select(method => $"{method} {endpoint.RoutePattern.RawText}"))
            .Order(StringComparer.Ordinal)
            .ToList();

        Assert.Equal(
            [
                $"GET {ProjectItemPattern}/board",
                $"PATCH {ProjectItemPattern}",
                $"POST {ProjectItemPattern}/tasks",
            ],
            routes);
    }

    /// <summary>
    /// AC11, Task 5 - the Tasks-per-Project bound is declared on at least one route, and only on routes whose
    /// pattern carries the Project the pipeline scopes the count by. A declaration on a route without it is
    /// the fail-closed case; this gate keeps it from reaching startup.
    /// </summary>
    [Fact]
    [Trait("Requirement", "AR-30")]
    [Trait("Requirement", "AD-25")]
    public void The_Tasks_per_Project_bound_is_declared_only_where_the_route_names_the_Project()
    {
        var declaring = RouteTable()
            .Where(endpoint => endpoint.Metadata.GetMetadata<RequiredBound>()?.Bound == Bound.TasksPerProject)
            .ToList();

        Assert.NotEmpty(declaring);
        Assert.All(declaring, endpoint => Assert.NotNull(endpoint.RoutePattern.GetParameter(Yello.Contracts.Projects.ProjectRoutes.ProjectIdParameter)));
    }

    /// <summary>
    /// AC3, UX-DR27, Task 10 - the client's predicate for offering Task creation agrees with the capability
    /// matrix for every Role: it offers it exactly where <c>ManageTasks</c> is not denied. Sibling of 2.1's
    /// ARCH-007 for <c>ManageProjects</c>.
    /// </summary>
    [Fact]
    [Trait("Requirement", "UX-DR27")]
    [Trait("Requirement", "AD-1")]
    public void The_clients_Task_controls_predicate_agrees_with_the_capability_matrix()
    {
        var predicate = ProductionAssemblies.Client.GetTypes()
            .SelectMany(type => type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static))
            .SingleOrDefault(method => method.Name.Contains("ManageTasks", StringComparison.Ordinal)
                && method.ReturnType == typeof(bool)
                && method.GetParameters() is [{ ParameterType: var only }]
                && only == typeof(string));

        Assert.True(predicate is not null, "No static bool (string roleCode) method named for ManageTasks in Yello.Client.");

        foreach (var role in Enum.GetValues<Role>())
        {
            var expected = RoleCapabilityMatrix.Decide(role, Capability.ManageTasks) != CapabilityDecision.Denied;

            Assert.True(
                predicate.Invoke(null, [StableCode.Of(role)]) is bool shown && shown == expected,
                $"The client's predicate disagrees with the matrix for {role}.");
        }
    }

    /// <summary>
    /// AC12, Task 8, <c>deferred-work.md:465-470</c> - no Host code answers through <c>Results.Created</c>,
    /// <c>TypedResults.Created</c>, <c>CreatedAtRoute</c> or <c>Accepted*</c>: the idempotency record reads
    /// <c>Location</c> from the response headers, which those factories write only when the result executes -
    /// after the record is taken - so a replay would answer with no <c>Location</c>. The convention in
    /// <c>ProjectEndpoints.CreateAsync</c>'s comment, held by a gate. <b>Active: green against 2.1's
    /// endpoints.</b>
    /// </summary>
    [Fact]
    [Trait("Requirement", "AR-25")]
    [Trait("Requirement", "AD-18")]
    public void No_endpoint_answers_through_Results_Created_or_Accepted()
    {
        // Vacuity guard: the same scan finds the Host answering through Results.Json, so it reads the calls.
        Assert.NotEmpty(ResultFactoryCallers(ProductionAssemblies.Host, ["Json"]));

        var offenders = ResultFactoryCallers(ProductionAssemblies.Host, BannedResultFactories);

        Assert.True(
            offenders.Count == 0,
            $"{string.Join(", ", offenders)} answer through Results.Created or Accepted. Set Location on the response "
            + "headers and answer Results.Json(..., 201), so the idempotency record replays it (deferred-work.md:465).");
    }

    /// <summary>
    /// The oracle for <see cref="No_endpoint_answers_through_Results_Created_or_Accepted"/>: the same scan,
    /// run over this assembly, reports the planted <see cref="PlantedCreatedAnswer"/>. A permanent plant, so a
    /// scan that stopped reading calls cannot pass.
    /// </summary>
    [Fact]
    [Trait("Requirement", "AR-25")]
    public void The_Results_Created_scan_reports_a_planted_caller()
    {
        var found = ResultFactoryCallers(typeof(PlantedCreatedAnswer).Assembly, BannedResultFactories);

        Assert.Contains(typeof(PlantedCreatedAnswer).FullName!.Replace('+', '/'), found);
    }

    /// <summary>
    /// AC12, Task 8 - every Space-scoped state-changing endpoint's handler is declared to return
    /// <see cref="IResult"/> (or a task of one): the record keeps a body only from an
    /// <see cref="IValueHttpResult"/>, so a handler returning a bare value would be served once and replayed
    /// empty, and a non-<see cref="IResult"/> would be a <c>500</c> only when keyed. <b>Active: green against
    /// 2.1's endpoints.</b>
    /// </summary>
    [Fact]
    [Trait("Requirement", "AR-25")]
    [Trait("Requirement", "AD-18")]
    public void Every_Space_scoped_state_changing_endpoint_answers_a_result_the_record_can_replay()
    {
        var handlers = RouteTable()
            .Where(CarriesSpaceId)
            .Where(endpoint => (endpoint.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods ?? []).Any(method => StateChangingMethods.Contains(method, StringComparer.Ordinal)))
            .Select(endpoint => (Route: endpoint.RoutePattern.RawText, Handler: endpoint.Metadata.GetMetadata<MethodInfo>()))
            .ToList();

        // Vacuity guard: 2.1's Project create is among them.
        Assert.Contains(handlers, handler => handler.Route == Yello.Contracts.Projects.ProjectRoutes.Create);

        var offenders = handlers
            .Where(handler => handler.Handler is null || !AnswersAResult(handler.Handler))
            .Select(handler => $"{handler.Route} ({handler.Handler?.ReturnType.Name ?? "no handler"})")
            .ToList();

        Assert.True(
            offenders.Count == 0,
            $"{string.Join(", ", offenders)} do not return IResult. The idempotency record captures a body only from an "
            + "IValueHttpResult (deferred-work.md:465).");
    }

    /// <summary>
    /// The oracle for <see cref="Every_Space_scoped_state_changing_endpoint_answers_a_result_the_record_can_replay"/>:
    /// the rule accepts <c>Task&lt;IResult&gt;</c> and <c>IResult</c>, and reports a handler returning a bare
    /// value.
    /// </summary>
    [Fact]
    [Trait("Requirement", "AR-25")]
    public void The_result_rule_reports_a_handler_returning_a_bare_value()
    {
        Assert.True(AnswersAResult(typeof(PlantedHandlers).GetMethod(nameof(PlantedHandlers.AnswersAsync))!));
        Assert.True(AnswersAResult(typeof(PlantedHandlers).GetMethod(nameof(PlantedHandlers.Answers))!));
        Assert.False(AnswersAResult(typeof(PlantedHandlers).GetMethod(nameof(PlantedHandlers.AnswersAValueAsync))!));
    }

    private static bool AnswersAResult(MethodInfo handler) =>
        handler.ReturnType == typeof(IResult)
        || handler.ReturnType == typeof(Task<IResult>)
        || handler.ReturnType == typeof(ValueTask<IResult>);

    /// <summary>
    /// Every type in <paramref name="assembly"/> whose IL calls one of <paramref name="names"/> on
    /// <c>Results</c> or <c>TypedResults</c>, by its Cecil full name (nested types joined with <c>/</c>).
    /// </summary>
    private static SortedSet<string> ResultFactoryCallers(Assembly assembly, string[] names)
    {
        var callers = new SortedSet<string>(StringComparer.Ordinal);

        using var module = ModuleDefinition.ReadModule(assembly.Location);

        foreach (var type in module.GetTypes())
        {
            var calls = type.Methods.Any(method => method.HasBody && method.Body.Instructions.Any(instruction =>
                instruction.Operand is MethodReference called
                && names.Contains(called.Name, StringComparer.Ordinal)
                && ResultFactoryTypes.Contains(called.DeclaringType.FullName, StringComparer.Ordinal)));

            if (calls)
            {
                callers.Add(OutermostOrSelf(type).FullName);
            }
        }

        return callers;
    }

    private static TypeDefinition OutermostOrSelf(TypeDefinition type) =>
        type.DeclaringType is { } declaring && declaring.Name.StartsWith('<') ? declaring : type;

    /// <summary>
    /// A planted caller of <c>Results.Created</c>, for the scan's oracle. Never mapped, never called.
    /// </summary>
    internal static class PlantedCreatedAnswer
    {
        public static IResult Answer() => Results.Created(new Uri("/planted", UriKind.Relative), value: null);
    }

    /// <summary>
    /// Planted handler shapes, for the result rule's oracle. Never mapped, never called.
    /// </summary>
    internal static class PlantedHandlers
    {
        public static Task<IResult> AnswersAsync() => Task.FromResult(Results.Ok());

        public static IResult Answers() => Results.Ok();

        public static Task<string> AnswersAValueAsync() => Task.FromResult("a bare value");
    }
}
