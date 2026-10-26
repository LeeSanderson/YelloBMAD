using System.Reflection;
using Microsoft.AspNetCore.Routing;
using Xunit;
using Yello.Domain.Memberships;
using Yello.Domain.Tasks;
using Yello.Host.Spaces;

namespace Yello.Tests.Architecture;

/// <summary>
/// Story 2.3's structural gates: the edit request carries the three attributes and nothing a client must never
/// write (AC1, AC6, AD-15); the due date is a <c>DateTimeOffset</c> wherever it is held (AC7, AR-34, A-15); the
/// by-id routes declare the capability the matrix gives them, serve both surfaces and consult no bound (AC3,
/// AC8, AD-4); and the client's title predicate is the Domain's rule (AC12). Written by the story 2.3 ATDD run.
/// </summary>
/// <remarks>
/// <para>
/// <b>Scaffolded against the recommended options</b>: Q1 (a) - the request is <c>EditTaskRequest</c> with
/// <c>Title</c>, <c>StatusId</c> and <c>DueDate</c>; the route is <c>TaskRoutes.Task</c>.
/// </para>
/// <para>
/// <b>One gate is green today</b> and stays active: <see cref="No_Domain_or_Contracts_member_is_typed_DateTime_or_DateOnly"/>,
/// because neither assembly holds one yet. It carries a permanent oracle over a planted type.
/// </para>
/// </remarks>
public sealed partial class RefusalBoundaryGateTests
{
    private static readonly Type[] BannedClockTypes = [typeof(DateTime), typeof(DateTime?), typeof(DateOnly), typeof(DateOnly?)];

    /// <summary>
    /// AC1, AC6, AD-15, Q1 (a) - beside the creation request, the Task contracts hold exactly one edit request,
    /// <c>EditTaskRequest</c>, whose properties are the title, the Status, the due date and - since story 2.4, Q1
    /// (a) - the Labels: no description (FR-31, Epic 7), no position key (readable, never writable), no Project
    /// (FR-41 is its own route) and no Space. The due date is <c>DateTimeOffset?</c>. Widened deliberately by story
    /// 2.4 by exactly <c>LabelIds</c>; its type is pinned by
    /// <c>The_Task_edit_request_carries_the_title_the_Status_the_due_date_and_the_Labels_and_nothing_else</c>.
    /// </summary>
    [Fact]
    [Trait("Requirement", "FR-20")]
    [Trait("Requirement", "AD-15")]
    [Trait("Requirement", "AD-12")]
    public void The_Task_edit_request_carries_its_editable_attributes_and_nothing_else()
    {
        var requests = ProductionAssemblies.Contracts.GetTypes()
            .Where(type => type.Namespace == TaskContractsNamespace && type.Name.EndsWith("Request", StringComparison.Ordinal))
            .ToList();

        Assert.Equal(["CreateTaskRequest", "EditTaskRequest"], requests.Select(type => type.Name).Order(StringComparer.Ordinal));

        var edit = requests.Single(type => type.Name == "EditTaskRequest");

        Assert.Equal(
            ["DueDate", "LabelIds", "StatusId", "Title"],
            edit.GetProperties(BindingFlags.Public | BindingFlags.Instance).Select(property => property.Name).Order(StringComparer.Ordinal));
        Assert.Equal(typeof(DateTimeOffset?), edit.GetProperty("DueDate")?.PropertyType);
    }

    /// <summary>
    /// AC7, AR-34, AD-19 - the due date is held as a <c>DateTimeOffset?</c> on the Task entity and on
    /// <c>TaskResponse</c>, and <c>TaskResponse</c> keeps every field 2.2 shipped (a field is added, none
    /// removed or retyped, FR-37). Story 2.4 appended <c>LabelIds</c> on the same terms, so the set is widened by
    /// exactly that name.
    /// </summary>
    [Fact]
    [Trait("Requirement", "AR-34")]
    [Trait("Requirement", "AD-19")]
    public void The_due_date_is_a_nullable_DateTimeOffset_on_the_Task_and_its_response()
    {
        var response = typeof(Yello.Contracts.Tasks.TaskResponse);

        Assert.Equal(typeof(DateTimeOffset?), typeof(TaskItem).GetProperty("DueDate")?.PropertyType);
        Assert.Equal(typeof(DateTimeOffset?), response.GetProperty("DueDate")?.PropertyType);
        Assert.Equal(
            ["CreatedAt", "DueDate", "Id", "LabelIds", "PositionKey", "StatusId", "Title"],
            response.GetProperties(BindingFlags.Public | BindingFlags.Instance).Select(property => property.Name).Order(StringComparer.Ordinal));
        Assert.Equal(typeof(DateTimeOffset), response.GetProperty("CreatedAt")?.PropertyType);
    }

    /// <summary>
    /// AC7, AR-34, A-15 - no public property, field, parameter or return value in the Domain or Contracts
    /// assemblies is a <c>DateTime</c> or a <c>DateOnly</c>: every instant is a <c>DateTimeOffset</c>, so a due
    /// date cannot arrive as a zone-less value. <b>Active: green before 2.3 writes anything</b>; the vacuity guard
    /// is that the same scan finds the <c>DateTimeOffset</c> members that exist.
    /// </summary>
    [Fact]
    [Trait("Requirement", "AR-34")]
    public void No_Domain_or_Contracts_member_is_typed_DateTime_or_DateOnly()
    {
        var types = ProductionAssemblies.Domain.GetTypes().Concat(ProductionAssemblies.Contracts.GetTypes()).ToList();

        // Vacuity guard: the scan reads members, so it finds the instants that are there.
        Assert.NotEmpty(ClockTypedMembers(types, [typeof(DateTimeOffset), typeof(DateTimeOffset?)]));

        Assert.Equal([], ClockTypedMembers(types, BannedClockTypes));
    }

    /// <summary>
    /// The oracle for <see cref="No_Domain_or_Contracts_member_is_typed_DateTime_or_DateOnly"/>: the same rule,
    /// run over a planted type, reports its <c>DateTime</c> property, its <c>DateOnly?</c> parameter and its
    /// <c>DateTime</c> return value.
    /// </summary>
    [Fact]
    [Trait("Requirement", "AR-34")]
    public void The_clock_type_scan_reports_a_planted_DateTime_member()
    {
        var found = ClockTypedMembers([typeof(PlantedClockMembers)], BannedClockTypes);

        Assert.Equal(
            [
                $"{typeof(PlantedClockMembers).FullName}.Due",
                $"{typeof(PlantedClockMembers).FullName}.Move(on)",
                $"{typeof(PlantedClockMembers).FullName}.Next()",
            ],
            found);
    }

    /// <summary>
    /// AC3, AC8, AD-4, Task 5 - the Task's own route is mapped for <c>GET</c> (requiring
    /// <c>ReadProjectsAndTasks</c>) and <c>PATCH</c> (requiring <c>ManageTasks</c>), neither is Session-only, and
    /// the <c>PATCH</c> consults no bound - an edit adds no Task, so counting the Project would refuse edits in a
    /// full one.
    /// </summary>
    [Fact]
    [Trait("Requirement", "AD-4")]
    [Trait("Requirement", "AD-1")]
    [Trait("Requirement", "AR-30")]
    public void The_Task_routes_declare_their_capability_serve_both_surfaces_and_consult_no_bound()
    {
        var routes = RouteTable()
            .Where(endpoint => endpoint.RoutePattern.RawText == Yello.Contracts.Tasks.TaskRoutes.Task)
            .SelectMany(endpoint => (endpoint.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods ?? []).Select(method => (Method: method, Endpoint: endpoint)))
            .ToDictionary(route => route.Method, route => route.Endpoint, StringComparer.Ordinal);

        Assert.Equal(["GET", "PATCH"], routes.Keys.Order(StringComparer.Ordinal));
        Assert.Equal(Capability.ReadProjectsAndTasks, routes["GET"].Metadata.GetMetadata<RequiredCapability>()?.Capability);
        Assert.Equal(Capability.ManageTasks, routes["PATCH"].Metadata.GetMetadata<RequiredCapability>()?.Capability);
        Assert.All(routes.Values, endpoint => Assert.DoesNotContain(endpoint.Metadata, metadata => metadata.GetType().Name == "SessionOnlyEndpoint"));
        Assert.Null(routes["PATCH"].Metadata.GetMetadata<RequiredBound>());
    }

    /// <summary>
    /// AC12, Task 9 - the client's title check before sending is the Domain's rule, mirrored (the client cannot
    /// reference Domain, AD-21): a static <c>bool (string)</c> method in <c>Yello.Client</c> named for the Task
    /// title agrees with <see cref="TaskTitle.TryNormalise"/> on every boundary - empty, blank, one character,
    /// padded, 200, 201, and 200 inside padding. The server still decides.
    /// </summary>
    [Fact]
    [Trait("Requirement", "AR-34")]
    [Trait("Requirement", "AD-21")]
    public void The_clients_Task_title_predicate_agrees_with_the_Domain_rule()
    {
        var predicate = ProductionAssemblies.Client.GetTypes()
            .SelectMany(type => type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static))
            .SingleOrDefault(method => method.Name.Contains("TaskTitle", StringComparison.Ordinal)
                && method.ReturnType == typeof(bool)
                && method.GetParameters() is [{ ParameterType: var only }]
                && only == typeof(string));

        Assert.True(predicate is not null, "No static bool (string) method named for TaskTitle in Yello.Client.");

        string[] inputs =
        [
            string.Empty,
            "   ",
            "a",
            "  a  ",
            new string('t', TaskTitle.MaxLength),
            new string('t', TaskTitle.MaxLength + 1),
            "  " + new string('t', TaskTitle.MaxLength) + "  ",
        ];

        foreach (var input in inputs)
        {
            var expected = TaskTitle.TryNormalise(input, out _);

            Assert.True(
                predicate.Invoke(null, [input]) is bool accepted && accepted == expected,
                $"The client's title predicate disagrees with TaskTitle for a {input.Length}-character input.");
        }
    }

    /// <summary>
    /// "Type.Member" for every public property or field typed as one of <paramref name="clockTypes"/>, and
    /// "Type.Method(parameter)" or "Type.Method()" for every public method taking or returning one.
    /// </summary>
    private static List<string> ClockTypedMembers(IEnumerable<Type> types, Type[] clockTypes)
    {
        const BindingFlags declared = BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
        var found = new List<string>();

        foreach (var type in types.Where(type => type.IsPublic || type.IsNestedPublic || type.IsNestedAssembly))
        {
            found.AddRange(type.GetProperties(declared).Where(property => clockTypes.Contains(property.PropertyType)).Select(property => $"{type.FullName}.{property.Name}"));
            found.AddRange(type.GetFields(declared).Where(field => clockTypes.Contains(field.FieldType)).Select(field => $"{type.FullName}.{field.Name}"));

            foreach (var method in type.GetMethods(declared).Where(method => !method.IsSpecialName))
            {
                found.AddRange(method.GetParameters().Where(parameter => clockTypes.Contains(parameter.ParameterType)).Select(parameter => $"{type.FullName}.{method.Name}({parameter.Name})"));

                if (clockTypes.Contains(method.ReturnType))
                {
                    found.Add($"{type.FullName}.{method.Name}()");
                }
            }
        }

        return [.. found.Order(StringComparer.Ordinal)];
    }

#pragma warning disable S6566 // Prefer DateTimeOffset - the plant is the very type the gate bans.
    /// <summary>
    /// Planted clock-typed members, for the scan's oracle. Never used.
    /// </summary>
    internal sealed class PlantedClockMembers
    {
        public DateTime Due { get; init; }

        public static DateTime Next() => DateTime.UnixEpoch;

        public static void Move(DateOnly? on) => _ = on;
    }
#pragma warning restore S6566
}
