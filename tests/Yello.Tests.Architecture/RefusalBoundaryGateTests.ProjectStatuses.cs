using System.Reflection;
using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Xunit;
using Yello.Domain;
using Yello.Domain.Memberships;
using Yello.Host.Spaces;
using Yello.Infrastructure.Persistence;

namespace Yello.Tests.Architecture;

/// <summary>
/// The structural gates of a Project's Status delta: its four routes declare the capability the matrix gives them and serve
/// both surfaces; nothing anywhere offers an inherit, override or reset mode; the four refusals have stable types; the
/// entry contract states its source and never a Space; the delta entity is Space-scoped, fixed at creation and holds
/// operations; the model declares the owning key NO ACTION and the name columns' collation; and the client's predicate for
/// the Project settings link and the editor's controls is the matrix's.
/// </summary>
/// <remarks>
/// <para>
/// <b>The subjects are named as strings</b> - the delta entity, the response type, the problem type members, the client
/// predicate - because none exists yet, so these compile today and fail for the stated reason when activated. The dev phase
/// swaps each for a typed reference in the change that adds it, as the Label gates did.
/// </para>
/// <para>
/// <b>Active gates that go red by design when the routes land</b>, and are amended deliberately in that change:
/// <c>The_routes_under_a_Project_are_its_deletion_its_rename_its_Task_creation_and_its_Board</c> (ARCH-503, widened by
/// exactly the four routes below), and <c>Every_Space_scoped_entity_the_gates_name_resolves_to_a_Domain_type</c> once the
/// delta entity is named in <c>SpaceScopedEntities</c>.
/// </para>
/// </remarks>
public sealed partial class RefusalBoundaryGateTests
{
    private static readonly string DeltaEntityName = typeof(Yello.Domain.Statuses.StatusDeltaOperation).FullName!;

    private static readonly string ProjectStatusResponseName = typeof(Yello.Contracts.Projects.ProjectStatusResponse).FullName!;

    private const string ProjectStatusesPattern = ProjectPattern + "/statuses";

    private const string ProjectStatusPattern = ProjectStatusesPattern + "/{statusId:guid}";

    private const string ProjectStatusPlacePattern = ProjectStatusPattern + "/place";

    private const string CaseInsensitiveNameCollation = "SQL_Latin1_General_CP1_CI_AS";

    private static readonly string[] ModeWords = ["Inherit", "Override", "Reset", "Mode"];

    /// <summary>
    /// The Project Status routes are exactly <c>GET</c> and <c>POST</c> on the collection, <c>PATCH</c> on one Status and
    /// <c>POST</c> on its <c>place</c> action: the read requires <c>ReadProjectsAndTasks</c> (a Viewer sees the set), every
    /// write requires <c>ConfigureProjectStatusDelta</c> (no new capability), none is Session-only, none consults a bound.
    /// </summary>
    [Fact]
    [Trait("Requirement", "FR-16")]
    [Trait("Requirement", "AD-4")]
    [Trait("Requirement", "FR-25")]
    public void The_Project_Status_routes_declare_their_capability_serve_both_surfaces_and_consult_no_bound()
    {
        var routes = RouteTable()
            .Where(endpoint => endpoint.RoutePattern.RawText is ProjectStatusesPattern or ProjectStatusPattern or ProjectStatusPlacePattern)
            .SelectMany(endpoint => (endpoint.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods ?? [])
                .Select(method => (Key: $"{method} {endpoint.RoutePattern.RawText}", Endpoint: endpoint)))
            .ToDictionary(route => route.Key, route => route.Endpoint, StringComparer.Ordinal);

        Assert.Equal(
            [$"GET {ProjectStatusesPattern}", $"PATCH {ProjectStatusPattern}", $"POST {ProjectStatusesPattern}", $"POST {ProjectStatusPlacePattern}"],
            routes.Keys.Order(StringComparer.Ordinal));
        Assert.Equal(Capability.ReadProjectsAndTasks, routes[$"GET {ProjectStatusesPattern}"].Metadata.GetMetadata<RequiredCapability>()?.Capability);

        foreach (var write in new[] { $"POST {ProjectStatusesPattern}", $"PATCH {ProjectStatusPattern}", $"POST {ProjectStatusPlacePattern}" })
        {
            Assert.Equal(Capability.ConfigureProjectStatusDelta, routes[write].Metadata.GetMetadata<RequiredCapability>()?.Capability);
        }

        Assert.All(routes.Values, endpoint => Assert.DoesNotContain(endpoint.Metadata, metadata => metadata.GetType().Name == "SessionOnlyEndpoint"));
        Assert.All(routes.Values, endpoint => Assert.Null(endpoint.Metadata.GetMetadata<RequiredBound>()));
    }

    /// <summary>
    /// There is no inherit or override mode, no toggle and no reset: anchored on the four Project Status routes existing, no
    /// mapped route under a Project and no member of the Project contracts' namespace names <c>Inherit</c>, <c>Override</c>,
    /// <c>Reset</c> or <c>Mode</c>.
    /// </summary>
    [Fact]
    [Trait("Requirement", "FR-25")]
    [Trait("Requirement", "AD-16")]
    public void No_route_or_contract_offers_an_inherit_override_or_reset_mode_for_a_Projects_Statuses()
    {
        var underProject = RouteTable()
            .Select(endpoint => endpoint.RoutePattern.RawText ?? string.Empty)
            .Where(pattern => pattern.StartsWith(ProjectPattern, StringComparison.Ordinal))
            .ToList();

        Assert.Contains(ProjectStatusesPattern, underProject);
        Assert.Contains(ProjectStatusPlacePattern, underProject);
        Assert.DoesNotContain(underProject, pattern => Array.Exists(ModeWords, word => pattern.Contains(word, StringComparison.OrdinalIgnoreCase)));

        var contractNames = ProductionAssemblies.Contracts.GetTypes()
            .Where(type => type.Namespace == typeof(Yello.Contracts.Projects.ProjectRoutes).Namespace)
            .SelectMany(type => new[] { type.Name }.Concat(type.GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly).Select(member => member.Name)))
            .ToList();

        Assert.Contains(contractNames, name => name.Contains("Status", StringComparison.Ordinal));
        Assert.DoesNotContain(contractNames, name => Array.Exists(ModeWords, word => name.Contains(word, StringComparison.Ordinal)));
    }

    /// <summary>
    /// The four refusals a Project Status write can meet have stable <c>urn:yello:problem:*</c> types on
    /// <c>ProblemTypes</c>: <c>invalid-status-name</c>, <c>status-name-taken</c>, <c>status-set-empty</c> and
    /// <c>invalid-status-position</c>.
    /// </summary>
    [Fact]
    [Trait("Requirement", "AR-34")]
    [Trait("Requirement", "FR-25")]
    public void The_Status_refusals_have_stable_problem_types()
    {
        var expected = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["InvalidStatusName"] = "urn:yello:problem:invalid-status-name",
            ["StatusNameTaken"] = "urn:yello:problem:status-name-taken",
            ["StatusSetEmpty"] = "urn:yello:problem:status-set-empty",
            ["InvalidStatusPosition"] = "urn:yello:problem:invalid-status-position",
        };

        foreach (var (member, type) in expected)
        {
            var property = typeof(Yello.Contracts.ProblemTypes).GetProperty(member, BindingFlags.Public | BindingFlags.Static);

            Assert.True(property is not null, $"ProblemTypes has no {member}.");
            Assert.Equal(type, property.GetValue(null));
        }
    }

    /// <summary>
    /// One entry of a Project's effective set is exactly <c>{ DefaultName, Id, Name, Source, TaskCount }</c>: the id that
    /// survives a rename, the effective name, where it comes from, the Space default's own name when renamed here (nullable),
    /// and the Task count - never a Space, never a position.
    /// </summary>
    [Fact]
    [Trait("Requirement", "UX-DR15")]
    [Trait("Requirement", "AD-3")]
    [Trait("Requirement", "AD-19")]
    public void The_Project_Status_entry_states_its_source_and_never_a_Space_or_a_position()
    {
        var response = ProductionAssemblies.Contracts.GetType(ProjectStatusResponseName);

        Assert.True(response is not null, $"No {ProjectStatusResponseName} in Yello.Contracts.");
        Assert.Equal(["DefaultName", "Id", "Name", "Source", "TaskCount"], PropertyNames(response));
        Assert.Equal(typeof(Guid), response.GetProperty("Id")?.PropertyType);
        Assert.Equal(typeof(int), response.GetProperty("TaskCount")?.PropertyType);
        Assert.True(
            new NullabilityInfoContext().Create(response.GetProperty("DefaultName")!).ReadState == NullabilityState.Nullable,
            "DefaultName must be nullable: it is set only for a default this Project renamed.");
    }

    /// <summary>
    /// The Space's own Status read keeps its shape, <c>{ Id, Name, Position }</c>: it serves the Space defaults only, and a
    /// Project's delta never reaches it. A standing gate, green before and after the delta: a served type is never
    /// changed in a breaking way.
    /// </summary>
    [Fact]
    [Trait("Requirement", "AD-19")]
    [Trait("Requirement", "FR-25")]
    public void The_Spaces_own_Status_read_keeps_its_shape()
    {
        var response = typeof(Yello.Contracts.Spaces.StatusResponse);

        Assert.Equal(["Id", "Name", "Position"], PropertyNames(response));
        Assert.Equal(typeof(Guid), response.GetProperty("Id")?.PropertyType);
    }

    /// <summary>
    /// The delta entity is Space-scoped and named in the gates' entity list, its Space and Project are fixed when it is
    /// created, and it carries one operation - <c>{ AfterStatusId, CreatedAt, Id, Kind, Name, ProjectId, Sequence, SpaceId,
    /// StatusId }</c> - with no navigation and no column holding a whole order.
    /// </summary>
    [Fact]
    [Trait("Requirement", "AD-2")]
    [Trait("Requirement", "AD-16")]
    public void The_delta_entity_is_Space_scoped_fixed_at_creation_and_holds_one_operation()
    {
        var entity = ProductionAssemblies.Domain.GetType(DeltaEntityName);

        Assert.True(entity is not null, $"No {DeltaEntityName} in Yello.Domain.");
        Assert.Contains(DeltaEntityName, SpaceScopedEntities);
        Assert.Equal(
            ["AfterStatusId", "CreatedAt", "Id", "Kind", "Name", "ProjectId", "Sequence", "SpaceId", "StatusId"],
            PropertyNames(entity));

        foreach (var fixedAtCreation in new[] { "SpaceId", "ProjectId" })
        {
            var setter = entity.GetProperty(fixedAtCreation, BindingFlags.Public | BindingFlags.Instance)?.SetMethod;

            Assert.NotNull(setter);
            Assert.Contains(typeof(IsExternalInit), setter.ReturnParameter.GetRequiredCustomModifiers());
        }
    }

    /// <summary>
    /// The model declares what the migration must carry: <c>StatusDefinition</c> has a nullable <c>OwnerProjectId</c> keyed
    /// <c>(SpaceId, OwnerProjectId)</c> to <c>Project</c> with NO ACTION (a cascade would be a second path from <c>Space</c>,
    /// error 1785), and the three name columns declare <c>SQL_Latin1_General_CP1_CI_AS</c> explicitly rather than resting on
    /// the database's default.
    /// </summary>
    [Fact]
    [Trait("Requirement", "AD-16")]
    [Trait("Requirement", "FR-25")]
    [Trait("Requirement", "FR-22")]
    public void The_owning_key_is_NO_ACTION_and_the_three_name_columns_declare_their_case_insensitive_collation_in_the_model()
    {
        using var context = new YelloDbContextFactory().CreateDbContext([]);

        // The design-time model: the read-optimised one the runtime uses drops a column's collation.
        var model = context.GetService<IDesignTimeModel>().Model;
        var statusDefinition = model.FindEntityType(typeof(Yello.Domain.Statuses.StatusDefinition));

        Assert.NotNull(statusDefinition);

        var owner = statusDefinition.FindProperty("OwnerProjectId");

        Assert.True(owner is not null && owner.IsNullable, "StatusDefinition has no nullable OwnerProjectId.");

        var owningKey = Assert.Single(statusDefinition.GetForeignKeys(), key => key.Properties.Any(property => property.Name == "OwnerProjectId"));

        Assert.Equal(DeleteBehavior.NoAction, owningKey.DeleteBehavior);
        Assert.Equal(["SpaceId", "OwnerProjectId"], owningKey.Properties.Select(property => property.Name));

        foreach (var entityName in new[] { typeof(Yello.Domain.Statuses.StatusDefinition).FullName!, typeof(Yello.Domain.Labels.Label).FullName!, DeltaEntityName })
        {
            var mapped = model.FindEntityType(entityName);

            Assert.True(mapped is not null, $"{entityName} is not in the model.");
            Assert.Equal(CaseInsensitiveNameCollation, mapped.FindProperty("Name")?.GetCollation());
        }
    }

    /// <summary>
    /// The client's predicate for the Project settings link and every Status-delta control agrees with the capability matrix
    /// for every Role: it offers them exactly where <c>ConfigureProjectStatusDelta</c> is not denied (Owner and Admin).
    /// </summary>
    [Fact]
    [Trait("Requirement", "UX-DR27")]
    [Trait("Requirement", "FR-16")]
    [Trait("Requirement", "AD-1")]
    public void The_clients_Project_Status_delta_predicate_agrees_with_the_capability_matrix()
    {
        var predicate = ProductionAssemblies.Client.GetTypes()
            .SelectMany(type => type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static))
            .SingleOrDefault(method => method.Name.Contains("ConfigureProjectStatusDelta", StringComparison.Ordinal)
                && method.ReturnType == typeof(bool)
                && method.GetParameters() is [{ ParameterType: var only }]
                && only == typeof(string));

        Assert.True(predicate is not null, "No static bool (string roleCode) method named for ConfigureProjectStatusDelta in Yello.Client.");

        foreach (var role in Enum.GetValues<Role>())
        {
            var expected = RoleCapabilityMatrix.Decide(role, Capability.ConfigureProjectStatusDelta) != CapabilityDecision.Denied;

            Assert.True(
                predicate.Invoke(null, [StableCode.Of(role)]) is bool shown && shown == expected,
                $"The client's Project Status delta predicate disagrees with the matrix for {role}.");
        }
    }
}
