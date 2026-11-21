using System.Reflection;
using Microsoft.AspNetCore.Routing;
using Xunit;
using Yello.Domain;
using Yello.Domain.Memberships;
using Yello.Host.Spaces;

namespace Yello.Tests.Architecture;

/// <summary>
/// The structural gates of changing the Space's default Statuses: the three writes sit beside the Space's own read, declare
/// the capability the matrix gives them and serve both surfaces; nothing under the Space's Statuses offers a removal, a
/// reset or a mode; the conflict refusal has a stable type and the problem body carries its report additively; the requests
/// carry exactly their members; the client's predicate is the matrix's; and the Space-level Status lock is named for the
/// request's Space.
/// </summary>
/// <remarks>
/// <para>
/// <b>The subjects are named as strings</b> - the request types, the problem type member, the lock's name method, the client
/// predicate - because none exists yet, so these compile today and fail for the stated reason when activated. The change
/// that adds each swaps the string for a typed reference.
/// </para>
/// <para>
/// <b>Active gates that go red by design in that change</b>, and are amended deliberately there:
/// <c>The_owned_Spaces_refusal_has_a_stable_type_and_an_appended_identity_only_Spaces_member</c> pins the problem body's
/// parameters exactly, and <c>The_matrix_theory_covers_exactly_the_capabilities_endpoints_declare</c> fails until the role
/// matrix theory gains a <c>ConfigureSpaceDefaultStatuses</c> row.
/// </para>
/// </remarks>
public sealed partial class RefusalBoundaryGateTests
{
    private const string SpaceStatusesPattern = "/api/v1/spaces/{spaceId:guid}/statuses";

    private const string SpaceStatusPattern = SpaceStatusesPattern + "/{statusId:guid}";

    private const string SpaceStatusPlacePattern = SpaceStatusPattern + "/place";

    private static readonly string[] SpaceStatusForbiddenWords = ["Remove", "Restore", "Delete", "Destination", "Reset", "Inherit", "Override", "Mode"];

    /// <summary>
    /// The Space's Status routes are exactly <c>GET</c> and <c>POST</c> on the collection, <c>PATCH</c> on one default and
    /// <c>POST</c> on its <c>place</c> action: the read keeps <c>ReadProjectsAndTasks</c>, every write requires
    /// <c>ConfigureSpaceDefaultStatuses</c>, none is Session-only and none consults a bound.
    /// </summary>
    [Fact(Skip = "red: AC2, AC12 - the Space-level write routes are not mapped yet (Q1)")]
    [Trait("Requirement", "FR-16")]
    [Trait("Requirement", "FR-24")]
    [Trait("Requirement", "AD-4")]
    public void The_Space_Status_routes_declare_their_capability_serve_both_surfaces_and_consult_no_bound()
    {
        var routes = RouteTable()
            .Where(endpoint => endpoint.RoutePattern.RawText is SpaceStatusesPattern or SpaceStatusPattern or SpaceStatusPlacePattern)
            .SelectMany(endpoint => (endpoint.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods ?? [])
                .Select(method => (Key: $"{method} {endpoint.RoutePattern.RawText}", Endpoint: endpoint)))
            .ToDictionary(route => route.Key, route => route.Endpoint, StringComparer.Ordinal);

        Assert.Equal(
            [$"GET {SpaceStatusesPattern}", $"PATCH {SpaceStatusPattern}", $"POST {SpaceStatusesPattern}", $"POST {SpaceStatusPlacePattern}"],
            routes.Keys.Order(StringComparer.Ordinal));
        Assert.Equal(Capability.ReadProjectsAndTasks, routes[$"GET {SpaceStatusesPattern}"].Metadata.GetMetadata<RequiredCapability>()?.Capability);

        foreach (var write in new[] { $"POST {SpaceStatusesPattern}", $"PATCH {SpaceStatusPattern}", $"POST {SpaceStatusPlacePattern}" })
        {
            Assert.Equal(Capability.ConfigureSpaceDefaultStatuses, routes[write].Metadata.GetMetadata<RequiredCapability>()?.Capability);
        }

        Assert.All(routes.Values, endpoint => Assert.DoesNotContain(endpoint.Metadata, metadata => metadata.GetType().Name == "SessionOnlyEndpoint"));
        Assert.All(routes.Values, endpoint => Assert.Null(endpoint.Metadata.GetMetadata<RequiredBound>()));
    }

    /// <summary>
    /// Nothing under the Space's Statuses removes a default, resets a Project to the defaults or offers a mode: anchored on the
    /// place route existing, no route under the collection answers <c>DELETE</c> or names a removal, a reset or a mode, and no
    /// Space Status request carries a member that could.
    /// </summary>
    [Fact(Skip = "red: AC1, AC3, AC8 - the Space-level write routes and requests do not exist yet (Q1)")]
    [Trait("Requirement", "FR-24")]
    [Trait("Requirement", "AD-16")]
    public void No_route_or_request_under_the_Spaces_Statuses_offers_a_removal_a_reset_or_a_mode()
    {
        var underStatuses = RouteTable()
            .Where(endpoint => endpoint.RoutePattern.RawText?.StartsWith(SpaceStatusesPattern, StringComparison.Ordinal) == true)
            .SelectMany(endpoint => (endpoint.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods ?? [])
                .Select(method => (Method: method, Pattern: endpoint.RoutePattern.RawText ?? string.Empty)))
            .ToList();

        Assert.Contains(underStatuses, route => route.Pattern == SpaceStatusPlacePattern);
        Assert.DoesNotContain(underStatuses, route => route.Method == "DELETE");
        Assert.DoesNotContain(underStatuses, route => Array.Exists(SpaceStatusForbiddenWords, word => route.Pattern.Contains(word, StringComparison.OrdinalIgnoreCase)));

        foreach (var name in new[] { "AddSpaceStatusRequest", "RenameSpaceStatusRequest", "PlaceSpaceStatusRequest" })
        {
            var request = SpaceContract(name);

            Assert.DoesNotContain(
                request.GetProperties(BindingFlags.Public | BindingFlags.Instance),
                property => Array.Exists(SpaceStatusForbiddenWords, word => property.Name.Contains(word, StringComparison.Ordinal)));
        }
    }

    /// <summary>
    /// The three requests carry exactly what their writes need, in <c>Yello.Contracts.Spaces</c>: an add
    /// <c>{ AfterStatusId, Name }</c>, a rename <c>{ Name, ReplaceProjectNames }</c> with a nullable decision, a place
    /// <c>{ AfterStatusId }</c> - never a Space id, a Project id or a position number.
    /// </summary>
    [Fact(Skip = "red: AC1, AC5, AC8 - the Space Status request contracts do not exist yet (Q1)")]
    [Trait("Requirement", "AD-2")]
    [Trait("Requirement", "AD-19")]
    [Trait("Requirement", "FR-27")]
    public void The_Space_Status_requests_carry_exactly_their_members()
    {
        var add = SpaceContract("AddSpaceStatusRequest");
        var rename = SpaceContract("RenameSpaceStatusRequest");
        var place = SpaceContract("PlaceSpaceStatusRequest");

        Assert.Equal(["AfterStatusId", "Name"], PropertyNames(add));
        Assert.Equal(typeof(Guid?), add.GetProperty("AfterStatusId")?.PropertyType);
        Assert.Equal(["Name", "ReplaceProjectNames"], PropertyNames(rename));
        Assert.Equal(typeof(bool?), rename.GetProperty("ReplaceProjectNames")?.PropertyType);
        Assert.Equal(["AfterStatusId"], PropertyNames(place));
        Assert.Equal(typeof(Guid?), place.GetProperty("AfterStatusId")?.PropertyType);
    }

    /// <summary>
    /// The conflict refusal has a stable type, <c>ProblemTypes.StatusRenameConflict</c> =
    /// <c>urn:yello:problem:status-rename-conflict</c>, and the existing Status refusals keep theirs.
    /// </summary>
    [Fact(Skip = "red: AC4 - ProblemTypes has no StatusRenameConflict yet (Q2)")]
    [Trait("Requirement", "AR-34")]
    [Trait("Requirement", "UX-DR16")]
    public void The_rename_conflict_refusal_has_a_stable_problem_type()
    {
        var type = typeof(Yello.Contracts.ProblemTypes).GetProperty("StatusRenameConflict", BindingFlags.Public | BindingFlags.Static);

        Assert.True(type is not null, "ProblemTypes has no StatusRenameConflict.");
        Assert.Equal("urn:yello:problem:status-rename-conflict", type.GetValue(null));
        Assert.Equal("urn:yello:problem:status-name-taken", Yello.Contracts.ProblemTypes.StatusNameTaken);
    }

    /// <summary>
    /// The problem body gains two nullable members after its existing ones, and changes nothing else: <c>Conflicts</c>, each
    /// <c>{ Name, ProjectId, ProjectName, TaskCount }</c>, and <c>Projects</c>, each <c>{ Id, Name }</c> - identity only.
    /// </summary>
    [Fact(Skip = "red: AC4, AC6 - ProblemResponse has no Conflicts or Projects yet (Q2, Q5)")]
    [Trait("Requirement", "AD-19")]
    [Trait("Requirement", "AR-34")]
    [Trait("Requirement", "UX-DR16")]
    public void The_problem_body_appends_a_nullable_conflict_report_and_a_nullable_Project_list()
    {
        var parameters = typeof(Yello.Contracts.ProblemResponse).GetConstructors().Single().GetParameters();

        Assert.Equal(["Type", "Errors", "Capability", "Bound", "Limit", "Spaces", "Conflicts", "Projects"], parameters.Select(parameter => parameter.Name));

        var conflicts = parameters[^2];
        var projects = parameters[^1];
        var nullability = new NullabilityInfoContext();

        Assert.Equal(NullabilityState.Nullable, nullability.Create(conflicts).ReadState);
        Assert.Equal(NullabilityState.Nullable, nullability.Create(projects).ReadState);
        Assert.Equal(["Name", "ProjectId", "ProjectName", "TaskCount"], PropertyNames(ElementOf(conflicts.ParameterType)));
        Assert.Equal(["Id", "Name"], PropertyNames(ElementOf(projects.ParameterType)));
    }

    /// <summary>
    /// The client's predicate for the Default Statuses section and every control in it agrees with the capability matrix for
    /// every Role: it offers them exactly where <c>ConfigureSpaceDefaultStatuses</c> is not denied (Owner and Admin).
    /// </summary>
    [Fact(Skip = "red: AC2 - Yello.Client has no ConfigureSpaceDefaultStatuses predicate yet")]
    [Trait("Requirement", "UX-DR27")]
    [Trait("Requirement", "FR-16")]
    [Trait("Requirement", "AD-1")]
    public void The_clients_Space_default_Status_predicate_agrees_with_the_capability_matrix()
    {
        var predicate = ClientRolePredicate("ConfigureSpaceDefaultStatuses");

        foreach (var role in Enum.GetValues<Role>())
        {
            var expected = RoleCapabilityMatrix.Decide(role, Capability.ConfigureSpaceDefaultStatuses) != CapabilityDecision.Denied;

            Assert.True(
                predicate.Invoke(null, [StableCode.Of(role)]) is bool shown && shown == expected,
                $"The client's Space default Status predicate disagrees with the matrix for {role}.");
        }
    }

    /// <summary>
    /// The Space-level Status lock is named for the request's Space, lower case, exactly as the race cases' holder spells it:
    /// <c>Yello/StatusDefaults/{spaceId:D}</c>. A different spelling would let the product and the holder take two locks and
    /// the race cases prove nothing.
    /// </summary>
    [Fact(Skip = "red: AC9 - ApplicationLocks has no StatusDefaults name yet (Q6)")]
    [Trait("Requirement", "AD-2")]
    [Trait("Requirement", "AD-17")]
    public void The_Status_defaults_lock_is_named_for_the_Space_as_the_race_holder_names_it()
    {
        var locks = ProductionAssemblies.Infrastructure.GetType("Yello.Infrastructure.Persistence.ApplicationLocks");

        Assert.NotNull(locks);

        var name = locks.GetMethod("StatusDefaults", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static, [typeof(Guid)]);

        Assert.True(name is not null && name.ReturnType == typeof(string), "ApplicationLocks has no static string StatusDefaults(Guid).");

        var spaceId = Guid.NewGuid();

        Assert.Equal($"Yello/StatusDefaults/{spaceId:D}", name.Invoke(null, [spaceId]));
    }

    private static Type SpaceContract(string name)
    {
        var fullName = typeof(Yello.Contracts.Spaces.SpaceRoutes).Namespace + "." + name;
        var type = ProductionAssemblies.Contracts.GetType(fullName);

        Assert.True(type is not null, $"No {fullName} in Yello.Contracts.");

        return type;
    }

    private static Type ElementOf(Type list)
    {
        var element = list.IsGenericType ? list.GetGenericArguments().SingleOrDefault() : list.GetElementType();

        Assert.True(element is not null, $"{list} is not a list of one element type.");

        return element;
    }
}
