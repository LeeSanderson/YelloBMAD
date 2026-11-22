using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Routing;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Xunit;
using Yello.Domain.Bounds;
using Yello.Domain.Memberships;
using Yello.Domain.Tasks;
using Yello.Host.Spaces;

namespace Yello.Tests.Architecture;

/// <summary>
/// The structural gates of moving every Task of one Status into another Project: one
/// <c>POST …/statuses/{statusId}/tasks/project</c> requiring <c>ManageTasks</c> on both surfaces and counting no bound in the
/// pipeline; its request names a Project and an optional Status and never a Space or a list of Tasks; its answer states
/// where the Tasks went and how many; the two cross-Project slices are the only ones admitted to count a bound; and a Task's
/// Project is written in exactly three places.
/// </summary>
/// <remarks>
/// <para>
/// <b>The subjects are found by name</b>, so a missing or renamed member fails here with the name it lost rather than as a
/// compile error in an unrelated gate. The route pattern stays a literal: it is what the gate asserts.
/// </para>
/// <para>
/// <b>The two bound gates here admit exactly the two cross-Project slices.</b> A dependency scan cannot pin call order, so
/// each slice's handler test pins the bound counted, the Project counted, and that it is counted after both locks.
/// </para>
/// <para>
/// <b>Both scans for a writer of <c>TaskItem.ProjectId</c> have an oracle</b> run over a planted type, so an empty or a
/// short result is never a scan that saw nothing.
/// </para>
/// </remarks>
public sealed partial class RefusalBoundaryGateTests
{
    private const string StatusTasksToProjectPattern = "/api/v1/spaces/{spaceId:guid}/projects/{projectId:guid}/statuses/{statusId:guid}/tasks/project";

    /// <summary>
    /// The second slice admitted to count a bound, for the reason the single move is: its destination is in the body, and a
    /// pipeline lock on it taken before the slice's sorted pair of Project locks would let two opposite moves deadlock.
    /// </summary>
    private const string MovesAStatusToAnotherProject = "Yello.Application.Tasks.MoveStatusTasksToProject.MoveStatusTasksToProjectHandler";

    private static readonly string TaskItemName = typeof(TaskItem).FullName!;

    /// <summary>
    /// <c>POST</c> is mapped on the bulk move route requiring <c>ManageTasks</c>, not Session-only, consulting no bound in the
    /// pipeline, and carrying exactly the Space, the source Project and the source Status in its path.
    /// </summary>
    [Fact]
    [Trait("Requirement", "FR-41")]
    [Trait("Requirement", "FR-35")]
    [Trait("Requirement", "AD-4")]
    [Trait("Requirement", "AD-25")]
    public void The_bulk_move_route_requires_ManageTasks_serves_both_surfaces_and_counts_no_bound_in_the_pipeline()
    {
        var route = Assert.Single(RouteTable(), endpoint => endpoint.RoutePattern.RawText == StatusTasksToProjectPattern);

        Assert.Equal(["POST"], route.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods ?? []);
        Assert.Equal(Capability.ManageTasks, route.Metadata.GetMetadata<RequiredCapability>()?.Capability);
        Assert.DoesNotContain(route.Metadata, metadata => metadata.GetType().FullName == SessionOnlyMarkerName);
        Assert.Null(route.Metadata.GetMetadata<RequiredBound>());
        Assert.True(CarriesSpaceId(route), "The bulk move route does not carry {spaceId}.");
        Assert.Equal(["projectId", "spaceId", "statusId"], route.RoutePattern.Parameters.Select(parameter => parameter.Name).Order(StringComparer.Ordinal));
    }

    /// <summary>
    /// <c>ProjectStatusRoutes</c> names the bulk move template once (<c>TasksToProject</c>), and <c>TasksToProjectOf</c> builds
    /// that path under the Status's own path.
    /// </summary>
    [Fact]
    [Trait("Requirement", "AR-35")]
    public void ProjectStatusRoutes_names_the_bulk_move_template_once()
    {
        var routes = typeof(Yello.Contracts.Projects.ProjectStatusRoutes);
        var template = routes.GetProperty("TasksToProject", BindingFlags.Public | BindingFlags.Static);
        var pathOf = routes.GetMethod("TasksToProjectOf", BindingFlags.Public | BindingFlags.Static, [typeof(Guid), typeof(Guid), typeof(Guid)]);

        Assert.True(template is not null, "ProjectStatusRoutes has no static TasksToProject property.");
        Assert.True(pathOf is not null, "ProjectStatusRoutes has no static TasksToProjectOf(Guid, Guid, Guid).");
        Assert.Equal(StatusTasksToProjectPattern, template.GetValue(null));

        var (spaceId, projectId, statusId) = (Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

        Assert.Equal(
            Yello.Contracts.Projects.ProjectStatusRoutes.ItemOf(spaceId, projectId, statusId) + "/tasks/project",
            pathOf.Invoke(null, [spaceId, projectId, statusId]));
    }

    /// <summary>
    /// The bulk move request names the destination Project and an optional destination Status and nothing else: no position,
    /// no Space, no list of Tasks. With the single move's request, it is one of exactly two Task requests that name a Project.
    /// </summary>
    [Fact]
    [Trait("Requirement", "FR-41")]
    [Trait("Requirement", "FR-19")]
    [Trait("Requirement", "AD-15")]
    public void The_bulk_move_request_names_a_Project_and_an_optional_Status_and_no_Space_or_Task_list()
    {
        var request = ProductionAssemblies.Contracts.GetType(TaskContractsNamespace + ".MoveStatusTasksToProjectRequest");

        Assert.True(request is not null, "Yello.Contracts.Tasks.MoveStatusTasksToProjectRequest does not exist.");
        Assert.Equal(
            ["ProjectId:Guid", "StatusId:Nullable`1"],
            request.GetProperties(BindingFlags.Public | BindingFlags.Instance).Select(property => $"{property.Name}:{property.PropertyType.Name}").Order(StringComparer.Ordinal));
        Assert.Equal(typeof(Guid?), request.GetProperty("StatusId")?.PropertyType);

        var namingAProject = TaskRequests()
            .Where(type => Array.Exists(type.GetProperties(BindingFlags.Public | BindingFlags.Instance), property => property.Name.Contains("Project", StringComparison.Ordinal)))
            .Select(type => type.Name);

        Assert.Equal(["MoveStatusTasksToProjectRequest", "MoveTaskToProjectRequest"], namingAProject);
        Assert.Equal([], SpaceMembersOf(TaskRequests()));
    }

    /// <summary>
    /// The bulk move answers where the Tasks went and how many: <c>{ projectId, statusId, movedCount }</c>, never the Tasks.
    /// </summary>
    [Fact]
    [Trait("Requirement", "FR-35")]
    [Trait("Requirement", "AD-19")]
    public void The_bulk_move_answer_states_the_destination_its_Status_and_the_count_moved()
    {
        var response = ProductionAssemblies.Contracts.GetType(TaskContractsNamespace + ".MoveStatusTasksToProjectResponse");

        Assert.True(response is not null, "Yello.Contracts.Tasks.MoveStatusTasksToProjectResponse does not exist.");
        Assert.Equal(
            ["MovedCount:Int32", "ProjectId:Guid", "StatusId:Guid"],
            response.GetProperties(BindingFlags.Public | BindingFlags.Instance).Select(property => $"{property.Name}:{property.PropertyType.Name}").Order(StringComparer.Ordinal));
    }

    /// <summary>
    /// The bulk move is one slice, <c>Yello.Application.Tasks.MoveStatusTasksToProject.MoveStatusTasksToProjectHandler</c>, a
    /// sealed class with a <c>HandleAsync</c>, beside the single move's.
    /// </summary>
    [Fact]
    [Trait("Requirement", "AR-3")]
    [Trait("Requirement", "AD-17")]
    public void The_bulk_move_is_one_slice()
    {
        var handler = ProductionAssemblies.Application.GetType(MovesAStatusToAnotherProject);

        Assert.True(handler is not null, $"{MovesAStatusToAnotherProject} does not exist.");
        Assert.True(handler.IsSealed, "MoveStatusTasksToProjectHandler is not sealed.");
        Assert.Contains(handler.GetMethods(BindingFlags.Public | BindingFlags.Instance), method => method.Name == "HandleAsync");
    }

    /// <summary>
    /// Nothing but the pipeline and the two cross-Project slices reads <c>BoundRegistry</c>, and both slices do: each counts
    /// its destination after its own sorted locks, because its destination is in the body. Their handler tests pin which
    /// bound is counted, for which Project, and after which lock.
    /// </summary>
    [Fact]
    [Trait("Priority", "P1")]
    [Trait("Requirement", "AR-3")]
    [Trait("Requirement", "AD-25")]
    public void Nothing_but_the_pipeline_and_the_two_cross_Project_slices_read_the_bound_registry()
    {
        var registry = typeof(BoundRegistry).FullName!;
        var dependents = DependentsOf(registry);
        string[] slices = [MovesATaskToAnotherProject, MovesAStatusToAnotherProject];

        Assert.Contains(PipelineTypeName, dependents);
        Assert.All(slices, slice => Assert.Contains(slice, dependents));

        string[] permitted = [registry, PipelineTypeName, .. slices];

        var offenders = dependents.Where(name => !permitted.Contains(name, StringComparer.Ordinal)).ToList();

        Assert.True(
            offenders.Count == 0,
            $"{string.Join(", ", offenders)} read BoundRegistry. A bound is declared once and checked by the pipeline, or by a cross-Project slice after its locks (AR-3, AD-25).");
    }

    /// <summary>
    /// Nothing but the pipeline, the port's adapter, its registration and the two cross-Project slices depends on the
    /// bound-count port, and both slices do.
    /// </summary>
    [Fact]
    [Trait("Priority", "P1")]
    [Trait("Requirement", "AR-3")]
    [Trait("Requirement", "AD-25")]
    public void Only_the_pipeline_and_the_two_cross_Project_slices_depend_on_the_bound_count_port()
    {
        string[] slices = [MovesATaskToAnotherProject, MovesAStatusToAnotherProject];

        Assert.All(slices, slice => Assert.Contains(slice, DependentsOf(BoundCountPortName)));
        AssertOnlyThePipelineDependsOn(
            BoundCountPortName,
            ["Yello.Infrastructure.Persistence.BoundCounter", "Yello.Infrastructure.SpaceBoundaryLimits", .. slices]);
    }

    /// <summary>
    /// A Task's Project is set through its setter in exactly two places: the creation's initialiser and the single move,
    /// both in the Task store. <c>TaskItem.ProjectId</c> has a public setter, so this gate is what keeps a third writer out.
    /// An <c>ExecuteUpdate</c> that sets it counts as a setter here.
    /// </summary>
    [Fact]
    [Trait("Requirement", "FR-19")]
    [Trait("Requirement", "AD-17")]
    public void Only_the_creation_and_the_single_move_set_a_Tasks_Project()
    {
        Assert.Equal(
            ["Yello.Infrastructure.Persistence.TaskStore.CreateAsync", "Yello.Infrastructure.Persistence.TaskStore.MoveToProjectAsync"],
            ProjectIdSetters(RuntimeAssemblies().Select(assembly => assembly.Location)));
    }

    /// <summary>
    /// The oracle for <see cref="Only_the_creation_and_the_single_move_set_a_Tasks_Project"/>: the same scan over a planted
    /// type reports its direct assignment and its <c>ExecuteUpdate</c>-shaped one, and not its read.
    /// </summary>
    [Fact]
    [Trait("Requirement", "FR-19")]
    public void The_Project_setter_scan_reports_a_planted_writer()
    {
        var planted = ProjectIdSetters([typeof(PlantedProjectWriter).Assembly.Location])
            .Where(name => name.StartsWith(typeof(PlantedProjectWriter).FullName!, StringComparison.Ordinal));

        Assert.Equal(
            [$"{typeof(PlantedProjectWriter).FullName}.{nameof(PlantedProjectWriter.Reparent)}", $"{typeof(PlantedProjectWriter).FullName}.{nameof(PlantedProjectWriter.ReparentAll)}"],
            planted);
    }

    /// <summary>
    /// The only raw SQL in production that sets a Task's Project is the set-based Status migration, which the bulk move shares
    /// with the Status removals; a predicate on <c>ProjectId</c> is not a write of it.
    /// </summary>
    [Fact]
    [Trait("Requirement", "FR-19")]
    [Trait("Requirement", "AD-17")]
    public void Only_the_Status_migration_sets_a_Tasks_Project_in_raw_SQL()
    {
        Assert.Equal(
            ["Yello.Infrastructure.Persistence.TaskStatusMigration"],
            ProjectIdSqlWriters(RuntimeAssemblies().Select(assembly => assembly.Location)));
    }

    /// <summary>
    /// The oracle for <see cref="Only_the_Status_migration_sets_a_Tasks_Project_in_raw_SQL"/>: the same scan over a planted
    /// type reports its statement that sets the Project, and not its statement that only filters on it.
    /// </summary>
    [Fact]
    [Trait("Requirement", "FR-19")]
    public void The_raw_SQL_scan_reports_a_planted_statement_that_sets_the_Project()
    {
        var planted = ProjectIdSqlWriters([typeof(PlantedProjectWriter).Assembly.Location], typeof(PlantedProjectWriter).FullName);

        Assert.Equal([typeof(PlantedProjectWriter).FullName!], planted);
        Assert.True(SetsProjectId(PlantedProjectWriter.Moves()), "The scan misses a SET of ProjectId.");
        Assert.False(SetsProjectId(PlantedProjectWriter.Filters()), "The scan reads a ProjectId predicate as a write.");
    }

    /// <summary>
    /// Every method that writes <c>TaskItem.ProjectId</c> - a call to its setter, or an <c>ExecuteUpdate</c> setter lambda
    /// naming it - as "outermost type.member", the member read through the compiler's async and lambda names.
    /// </summary>
    private static SortedSet<string> ProjectIdSetters(IEnumerable<string> locations)
    {
        var writers = new SortedSet<string>(StringComparer.Ordinal);

        foreach (var location in locations)
        {
            using var module = ModuleDefinition.ReadModule(location);

            foreach (var (type, method) in module.GetTypes().SelectMany(type => type.Methods.Where(WritesProjectId).Select(method => (type, method))))
            {
                writers.Add($"{OutermostOf(type).FullName}.{SourceMemberOf(type, method)}");
            }
        }

        return writers;
    }

    /// <summary>
    /// Whether a method calls <c>TaskItem.ProjectId</c>'s setter, or builds a <c>SetProperty</c> call over its getter.
    /// </summary>
    private static bool WritesProjectId(MethodDefinition method)
    {
        if (!method.HasBody)
        {
            return false;
        }

        var calls = method.Body.Instructions.Select(instruction => instruction.Operand).OfType<MethodReference>().ToList();
        var setsDirectly = calls.Exists(called => called.Name == "set_ProjectId" && called.DeclaringType.FullName == TaskItemName);

        return setsDirectly || (calls.Exists(called => called.Name == "SetProperty")
            && method.Body.Instructions.Any(instruction => instruction.OpCode == OpCodes.Ldtoken
                && instruction.Operand is MethodReference { Name: "get_ProjectId" } getter
                && getter.DeclaringType.FullName == TaskItemName));
    }

    /// <summary>
    /// Every type whose IL holds a string literal that sets <c>ProjectId</c> in a <c>SET</c> list, by outermost type; only
    /// types under <paramref name="within"/> when it is given.
    /// </summary>
    private static SortedSet<string> ProjectIdSqlWriters(IEnumerable<string> locations, string? within = null)
    {
        var writers = new SortedSet<string>(StringComparer.Ordinal);

        foreach (var location in locations)
        {
            using var module = ModuleDefinition.ReadModule(location);

            foreach (var type in module.GetTypes().Where(type => within is null || OutermostOf(type).FullName == within))
            {
                var writes = type.Methods.Any(method => method.HasBody && method.Body.Instructions.Any(instruction =>
                    instruction.OpCode == OpCodes.Ldstr && instruction.Operand is string literal && SetsProjectId(literal)));

                if (writes)
                {
                    writers.Add(OutermostOf(type).FullName);
                }
            }
        }

        return writers;
    }

    /// <summary>
    /// Whether a statement assigns <c>ProjectId</c> in a <c>SET</c> list - the text between <c>SET</c> and the next
    /// <c>FROM</c>, <c>WHERE</c> or <c>OUTPUT</c>.
    /// </summary>
    private static bool SetsProjectId(string statement) =>
        SetList.Matches(statement).Any(match => ProjectIdAssignment.IsMatch(match.Groups["list"].Value));

    /// <summary>
    /// The member a compiler-generated method was written in: <c>&lt;Name&gt;d__N</c> for an async state machine,
    /// <c>&lt;Name&gt;b__N</c> for a lambda; otherwise the method itself.
    /// </summary>
    private static string SourceMemberOf(TypeDefinition type, MethodDefinition method)
    {
        for (var current = type; current is not null; current = current.DeclaringType)
        {
            var stateMachine = CompilerGeneratedName.Match(current.Name);

            if (stateMachine.Success && stateMachine.Groups["kind"].Value == "d")
            {
                return stateMachine.Groups["name"].Value;
            }
        }

        var lambda = CompilerGeneratedName.Match(method.Name);

        return lambda.Success ? lambda.Groups["name"].Value : method.Name;
    }

    [GeneratedRegex(@"\bSET\b(?<list>(?:(?!\b(?:FROM|WHERE|OUTPUT)\b).)*)", RegexOptions.IgnoreCase | RegexOptions.Singleline, matchTimeoutMilliseconds: 5000)]
    private static partial Regex SetList { get; }

    [GeneratedRegex(@"(?:^|[\s,.\[])ProjectId\]?\s*=", RegexOptions.None, matchTimeoutMilliseconds: 5000)]
    private static partial Regex ProjectIdAssignment { get; }

    [GeneratedRegex(@"^<(?<name>[^>]+)>(?<kind>[a-z])__", RegexOptions.None, matchTimeoutMilliseconds: 5000)]
    private static partial Regex CompilerGeneratedName { get; }
}

/// <summary>
/// A planted writer of a Task's Project, for the scans' oracles: a direct assignment, an <c>ExecuteUpdate</c>-shaped one, and
/// two statements, one that sets the Project and one that only filters on it.
/// </summary>
internal static class PlantedProjectWriter
{
    public static void Reparent(TaskItem task, Guid projectId) => task.ProjectId = projectId;

    public static void ReparentAll(IEnumerable<TaskItem> tasks, Guid projectId) =>
        SetProperty(task => task.ProjectId, projectId, tasks);

    public static string Moves() => "UPDATE t SET StatusId = {0}, ProjectId = {1}, PositionKey = m.[Key] FROM dbo.[Task] AS t WHERE t.Id = {2}";

    public static string Filters() => "UPDATE t SET StatusId = {0}, PositionKey = m.[Key] FROM dbo.[Task] AS t WHERE t.ProjectId = {1}";

    private static void SetProperty(System.Linq.Expressions.Expression<Func<TaskItem, Guid>> property, Guid value, IEnumerable<TaskItem> tasks)
    {
        if (property.Body is not System.Linq.Expressions.MemberExpression { Member: PropertyInfo setter })
        {
            return;
        }

        foreach (var task in tasks)
        {
            setter.SetValue(task, value);
        }
    }
}
