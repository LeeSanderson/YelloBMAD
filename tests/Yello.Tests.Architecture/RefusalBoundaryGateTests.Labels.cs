using System.Reflection;
using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.Routing;
using Xunit;
using Yello.Domain;
using Yello.Domain.Memberships;
using Yello.Host.Spaces;

namespace Yello.Tests.Architecture;

/// <summary>
/// Story 2.4's structural gates: the Label routes declare the capability the matrix gives them, serve both surfaces
/// and consult no bound (AC8, AC9, AD-4, Q6); the Label entity is per Space and fixed there, with no navigation
/// (AC6, AD-2); the gates' own entity list names a Label that now exists (AC8); the Label contracts carry no Space
/// (AC11, AD-3); the Task contracts gain <c>labelIds</c> and nothing else (AC7, FR-20, AD-19); and the client's
/// Label-definition predicate is the matrix's (AC1, UX-DR27). Written by the story 2.4 ATDD run.
/// </summary>
/// <remarks>
/// <para>
/// <b>The subjects are typed references</b> since story 2.4's dev phase (the entity, the contracts' namespace), so a
/// rename breaks the build rather than emptying the gate; the entity's name stays stated, because the gates' own
/// entity list names it as a string. The route patterns stay literals: they are what the gate asserts.
/// </para>
/// <para>
/// <b>Two active 2.3 gates go red by design when Task 4 lands</b> and are amended deliberately in that change:
/// <c>The_Task_edit_request_carries_its_editable_attributes_and_nothing_else</c> (ARCH-006 below is its
/// successor) and <c>The_due_date_is_a_nullable_DateTimeOffset_on_the_Task_and_its_response</c>'s property set
/// (ARCH-005 is its successor). <c>Every_Space_scoped_entity_the_gates_name_resolves_to_a_Domain_type</c> loses its
/// <c>Label</c> exclusion in the change that adds the type (ARCH-003 is that gate without it).
/// </para>
/// </remarks>
public sealed partial class RefusalBoundaryGateTests
{
    private const string LabelEntityName = "Yello.Domain.Labels.Label";

    private static readonly string LabelContractsNamespace = typeof(Yello.Contracts.Labels.LabelResponse).Namespace!;

    private const string LabelsPattern = "/api/v1/spaces/{spaceId:guid}/labels";

    private const string LabelPattern = "/api/v1/spaces/{spaceId:guid}/labels/{labelId:guid}";

    /// <summary>
    /// AC8, AC9, AD-4, Q6, Task 5 - the Label routes are mapped as <c>GET</c> and <c>POST</c> on the collection and
    /// <c>DELETE</c> on the item: the list requires <c>ReadProjectsAndTasks</c> (a Viewer sees chips), definition and
    /// deletion require <c>ManageLabels</c> (no new capability), none is Session-only, and none consults a bound.
    /// </summary>
    [Fact]
    [Trait("Requirement", "AD-4")]
    [Trait("Requirement", "AD-1")]
    [Trait("Requirement", "FR-22")]
    public void The_Label_routes_declare_their_capability_serve_both_surfaces_and_consult_no_bound()
    {
        var routes = RouteTable()
            .Where(endpoint => endpoint.RoutePattern.RawText is LabelsPattern or LabelPattern)
            .SelectMany(endpoint => (endpoint.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods ?? [])
                .Select(method => (Key: $"{method} {endpoint.RoutePattern.RawText}", Endpoint: endpoint)))
            .ToDictionary(route => route.Key, route => route.Endpoint, StringComparer.Ordinal);

        Assert.Equal(
            [$"DELETE {LabelPattern}", $"GET {LabelsPattern}", $"POST {LabelsPattern}"],
            routes.Keys.Order(StringComparer.Ordinal));
        Assert.Equal(Capability.ReadProjectsAndTasks, routes[$"GET {LabelsPattern}"].Metadata.GetMetadata<RequiredCapability>()?.Capability);
        Assert.Equal(Capability.ManageLabels, routes[$"POST {LabelsPattern}"].Metadata.GetMetadata<RequiredCapability>()?.Capability);
        Assert.Equal(Capability.ManageLabels, routes[$"DELETE {LabelPattern}"].Metadata.GetMetadata<RequiredCapability>()?.Capability);
        Assert.All(routes.Values, endpoint => Assert.DoesNotContain(endpoint.Metadata, metadata => metadata.GetType().Name == "SessionOnlyEndpoint"));
        Assert.All(routes.Values, endpoint => Assert.Null(endpoint.Metadata.GetMetadata<RequiredBound>()));
    }

    /// <summary>
    /// AC6, AD-2 - <c>Yello.Domain.Labels.Label</c> exists under exactly that name (the gates already name it), its Space
    /// can only be set when it is created, and it carries exactly <c>Id</c>, <c>SpaceId</c>, <c>Name</c>,
    /// <c>Colour</c> and <c>CreatedAt</c> - no navigation property, as the model has none by design.
    /// </summary>
    [Fact]
    [Trait("Requirement", "AD-2")]
    [Trait("Requirement", "FR-22")]
    public void A_Labels_Space_can_only_be_set_when_it_is_created_and_it_carries_no_navigation()
    {
        var label = typeof(Yello.Domain.Labels.Label);

        Assert.Equal(LabelEntityName, label.FullName);

        var setter = label.GetProperty("SpaceId", BindingFlags.Public | BindingFlags.Instance)?.SetMethod;

        Assert.NotNull(setter);
        Assert.Contains(typeof(IsExternalInit), setter.ReturnParameter.GetRequiredCustomModifiers());
        Assert.Equal(
            ["Colour", "CreatedAt", "Id", "Name", "SpaceId"],
            label.GetProperties(BindingFlags.Public | BindingFlags.Instance).Select(property => property.Name).Order(StringComparer.Ordinal));
        Assert.Equal(typeof(DateTimeOffset), label.GetProperty("CreatedAt")?.PropertyType);
    }

    /// <summary>
    /// AC8, AR-9 - every name in <c>SpaceScopedEntities</c>, <c>Label</c> included, resolves to a type in the Domain
    /// assembly, so AR-9's <c>No_endpoint_without_spaceId_in_its_path_reaches_a_Space_scoped_entity</c> covers the Label
    /// for real. The successor of 2.2's gate with its temporary exclusion removed.
    /// </summary>
    [Fact]
    [Trait("Requirement", "AD-2")]
    [Trait("Requirement", "AD-3")]
    public void Every_Space_scoped_entity_the_gates_name_resolves_to_a_Domain_type_Label_included()
    {
        Assert.Contains(LabelEntityName, SpaceScopedEntities);
        Assert.Equal([], SpaceScopedEntities.Where(name => ProductionAssemblies.Domain.GetType(name) is null));
    }

    /// <summary>
    /// AC11, AD-3, Task 4 - the Label contracts live in their own namespace (the Task and Board response gate pins its
    /// namespaces to exactly four): <c>LabelResponse</c> is exactly <c>{Colour, Id, Name}</c>,
    /// <c>CreateLabelRequest</c> exactly <c>{Colour, Name}</c>, and no Label contract carries a <c>SpaceId</c>.
    /// </summary>
    [Fact]
    [Trait("Requirement", "AD-3")]
    [Trait("Requirement", "FR-22")]
    public void The_Label_contracts_carry_a_name_a_colour_and_an_id_and_never_a_Space()
    {
        var contracts = ProductionAssemblies.Contracts.GetTypes()
            .Where(type => type.Namespace == LabelContractsNamespace && (type.Name.EndsWith("Request", StringComparison.Ordinal) || type.Name.EndsWith("Response", StringComparison.Ordinal)))
            .ToDictionary(type => type.Name, StringComparer.Ordinal);

        Assert.Equal(["CreateLabelRequest", "LabelResponse"], contracts.Keys.Order(StringComparer.Ordinal));
        Assert.Equal(["Colour", "Id", "Name"], PropertyNames(contracts["LabelResponse"]));
        Assert.Equal(["Colour", "Name"], PropertyNames(contracts["CreateLabelRequest"]));
        Assert.Equal(typeof(Guid), contracts["LabelResponse"].GetProperty("Id")?.PropertyType);
    }

    /// <summary>
    /// AC7, AD-19, FR-37 - <c>TaskResponse</c> gains <c>LabelIds</c> as <c>IReadOnlyList&lt;Guid&gt;</c> and keeps every
    /// field 2.2 and 2.3 shipped: a field added, none removed or retyped. The successor of 2.3's response-shape assert.
    /// </summary>
    [Fact]
    [Trait("Requirement", "AD-19")]
    [Trait("Requirement", "FR-20")]
    public void The_Task_response_gains_its_Label_ids_and_keeps_every_earlier_field()
    {
        var response = typeof(Yello.Contracts.Tasks.TaskResponse);

        Assert.Equal(["CreatedAt", "DueDate", "Id", "LabelIds", "PositionKey", "StatusId", "Title"], PropertyNames(response));
        Assert.Equal(typeof(IReadOnlyList<Guid>), response.GetProperty("LabelIds")?.PropertyType);
        Assert.Equal(typeof(DateTimeOffset?), response.GetProperty("DueDate")?.PropertyType);
    }

    /// <summary>
    /// AC7, FR-20, Q1 (a) - the Task edit request is exactly <c>{DueDate, LabelIds, StatusId, Title}</c>: still no
    /// description, position key, Project or Space; <c>LabelIds</c> is the whole set, an <c>IReadOnlyList&lt;Guid&gt;</c>.
    /// The successor of 2.3's <c>The_Task_edit_request_carries_its_editable_attributes_and_nothing_else</c>.
    /// </summary>
    [Fact]
    [Trait("Requirement", "FR-20")]
    [Trait("Requirement", "AD-15")]
    public void The_Task_edit_request_carries_the_title_the_Status_the_due_date_and_the_Labels_and_nothing_else()
    {
        var edit = typeof(Yello.Contracts.Tasks.EditTaskRequest);

        Assert.Equal(["DueDate", "LabelIds", "StatusId", "Title"], PropertyNames(edit));
        Assert.Equal(typeof(IReadOnlyList<Guid>), edit.GetProperty("LabelIds")?.PropertyType);
    }

    /// <summary>
    /// AC1, AC14, UX-DR27, Task 10 - the client's predicate for offering Label definition (the Space settings link and
    /// the define form) agrees with the capability matrix for every Role: it offers it exactly where
    /// <c>ManageLabels</c> is not denied. Sibling of 2.2's <c>The_clients_Task_controls_predicate_agrees_with_the_capability_matrix</c>.
    /// </summary>
    [Fact]
    [Trait("Requirement", "UX-DR27")]
    [Trait("Requirement", "AD-1")]
    public void The_clients_Label_definition_predicate_agrees_with_the_capability_matrix()
    {
        var predicate = ProductionAssemblies.Client.GetTypes()
            .SelectMany(type => type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static))
            .SingleOrDefault(method => method.Name.Contains("ManageLabels", StringComparison.Ordinal)
                && method.ReturnType == typeof(bool)
                && method.GetParameters() is [{ ParameterType: var only }]
                && only == typeof(string));

        Assert.True(predicate is not null, "No static bool (string roleCode) method named for ManageLabels in Yello.Client.");

        foreach (var role in Enum.GetValues<Role>())
        {
            var expected = RoleCapabilityMatrix.Decide(role, Capability.ManageLabels) != CapabilityDecision.Denied;

            Assert.True(
                predicate.Invoke(null, [StableCode.Of(role)]) is bool shown && shown == expected,
                $"The client's Label predicate disagrees with the matrix for {role}.");
        }
    }

    /// <summary>
    /// Q2, Task 10 - the client's courtesy check of a Label name agrees with the Domain's <c>LabelName</c> at every edge
    /// (empty, blank, one character, trimmed, 50 and 51 characters). Added by the dev phase with the define form; the
    /// sibling of 2.3's <c>The_clients_Task_title_predicate_agrees_with_the_Domain_rule</c>.
    /// </summary>
    [Fact]
    [Trait("Requirement", "FR-22")]
    [Trait("Assumption", "FR-22-Q2")]
    public void The_clients_Label_name_predicate_agrees_with_the_Domain_rule()
    {
        var predicate = ProductionAssemblies.Client.GetTypes()
            .SelectMany(type => type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static))
            .SingleOrDefault(method => method.Name.Contains("LabelName", StringComparison.Ordinal)
                && method.ReturnType == typeof(bool)
                && method.GetParameters() is [{ ParameterType: var only }]
                && only == typeof(string));

        Assert.True(predicate is not null, "No static bool (string) method named for LabelName in Yello.Client.");

        string[] inputs =
        [
            string.Empty,
            "   ",
            "a",
            "  a  ",
            new string('n', Yello.Domain.Labels.LabelName.MaxLength),
            new string('n', Yello.Domain.Labels.LabelName.MaxLength + 1),
            "  " + new string('n', Yello.Domain.Labels.LabelName.MaxLength) + "  ",
        ];

        foreach (var input in inputs)
        {
            var expected = Yello.Domain.Labels.LabelName.TryNormalise(input, out _);

            Assert.True(
                predicate.Invoke(null, [input]) is bool accepted && accepted == expected,
                $"The client's Label name predicate disagrees with LabelName for a {input.Length}-character input.");
        }
    }

    private static string[] PropertyNames(Type type) =>
        [.. type.GetProperties(BindingFlags.Public | BindingFlags.Instance).Select(property => property.Name).Order(StringComparer.Ordinal)];
}
