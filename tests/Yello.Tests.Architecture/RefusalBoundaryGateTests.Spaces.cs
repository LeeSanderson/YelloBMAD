using System.Reflection;
using Microsoft.AspNetCore.Routing;
using Xunit;
using Yello.Domain;
using Yello.Domain.Bounds;
using Yello.Domain.Memberships;
using Yello.Host.Spaces;

namespace Yello.Tests.Architecture;

/// <summary>
/// Story 3.1's structural gates for creating and renaming a Space: the client's rename and Space settings predicates are
/// the matrix's (AC3, AC4, UX-DR27); the two request contracts carry a name and nothing else, so the new Space's id is
/// never the body's (AC9, AD-2); the Space name rule is 1-200 and the column agrees (AC9, Q6); the Spaces-per-Account
/// bound is consulted by the creation route alone (AC6, AD-25); and rename declares <c>RenameSpace</c> while creation
/// declares no capability at all (AC10, FR-16). Written by the story 3.1 ATDD run, 2026-10-29.
/// </summary>
/// <remarks>
/// <para>
/// <b>RED-PHASE SCAFFOLDS.</b> Every case is skipped with its AC. None of the subjects exist yet, so they are reached
/// by name (<c>Yello.Contracts.Spaces.CreateSpaceRequest</c>, <c>Yello.Domain.Spaces.SpaceName</c>, a client predicate
/// named for <c>RenameSpace</c> or <c>SpaceSettings</c>) - the story's indicative names (Tasks 3, 4, 7). On activation,
/// replace each lookup by the typed reference it stands for, so a rename is a compile error.
/// </para>
/// <para>
/// <b>Active gates that go red by design when 3.1 lands</b> and are amended in that change (Task 9), not here:
/// <c>No_endpoint_without_spaceId_in_its_path_reaches_a_Space_scoped_entity</c> (creation is the second named
/// exemption beside registration), <c>Every_Account_scoped_endpoint_names_an_AD_24_surface</c> and
/// <c>SpaceSwitcherGateTests.The_Account_scoped_surfaces_are_exactly_the_two_AD_24_names</c> (if Q1's step carries a
/// surface member), <c>SpaceSwitcherGateTests.No_caller_sets_both_session_context_keys</c> (its successor is
/// <c>Exactly_one_creation_unit_of_work_sets_both_session_context_keys</c>), <c>Space_is_read_only_where_named</c>
/// (the creation and rename stores), and <c>SpaceSwitcherGateTests.Only_the_Account_scoped_handlers_depend_on_the_Account_scoped_reads</c>
/// if the creation handler takes an Account-scoped read.
/// </para>
/// </remarks>
public sealed partial class RefusalBoundaryGateTests
{
    private const string CreateSpaceRequestName = "Yello.Contracts.Spaces.CreateSpaceRequest, Yello.Contracts";

    private const string RenameSpaceRequestName = "Yello.Contracts.Spaces.RenameSpaceRequest, Yello.Contracts";

    private const string SpaceNameTypeName = "Yello.Domain.Spaces.SpaceName";

    private const string SpaceConfigurationTypeName = "Yello.Infrastructure.Persistence.Configurations.SpaceConfiguration";

    /// <summary>
    /// Story 3.1 Q6 (a), assumed (open for the dev phase): the longest provisioned name is 128 + "'s Space", so 200.
    /// </summary>
    private const int SpaceNameLimit = 200;

    /// <summary>
    /// AC3, AC4, UX-DR27, Task 7 - the client's predicate for offering rename agrees with the capability matrix for
    /// every Role: it offers it exactly where <c>RenameSpace</c> is not denied (Owner, Admin). Sibling of
    /// <c>The_clients_Label_definition_predicate_agrees_with_the_capability_matrix</c>.
    /// </summary>
    [Fact(Skip = "red: AC3 - SpaceCapabilities.PermitsRenameSpace does not exist yet (story 3.1 Task 7)")]
    [Trait("Requirement", "UX-DR27")]
    [Trait("Requirement", "FR-6")]
    public void The_clients_rename_predicate_agrees_with_the_capability_matrix()
    {
        var predicate = ClientRolePredicate("RenameSpace");

        foreach (var role in Enum.GetValues<Role>())
        {
            var expected = RoleCapabilityMatrix.Decide(role, Capability.RenameSpace) != CapabilityDecision.Denied;

            Assert.True(
                predicate.Invoke(null, [StableCode.Of(role)]) is bool shown && shown == expected,
                $"The client's rename predicate disagrees with the matrix for {role}.");
        }
    }

    /// <summary>
    /// AC4, UX-DR27, Q4 (a) assumed - the context bar's Space settings link is offered exactly where at least one of
    /// its sections is (the Label section's <c>ManageLabels</c>, the name section's <c>RenameSpace</c>), so the
    /// link's audience is defined in one place and is never wider or narrower than the page.
    /// </summary>
    [Fact(Skip = "red: AC4 - SpaceCapabilities.PermitsSpaceSettings does not exist yet (story 3.1 Task 7)")]
    [Trait("Requirement", "UX-DR27")]
    [Trait("Requirement", "UX-DR9")]
    public void The_clients_Space_settings_predicate_is_the_union_of_its_sections()
    {
        var predicate = ClientRolePredicate("SpaceSettings");

        foreach (var role in Enum.GetValues<Role>())
        {
            var expected = RoleCapabilityMatrix.Decide(role, Capability.RenameSpace) != CapabilityDecision.Denied
                || RoleCapabilityMatrix.Decide(role, Capability.ManageLabels) != CapabilityDecision.Denied;

            Assert.True(
                predicate.Invoke(null, [StableCode.Of(role)]) is bool shown && shown == expected,
                $"The client's Space settings predicate disagrees with its sections for {role}.");
        }
    }

    /// <summary>
    /// AC9, AD-2, Task 4 - <c>CreateSpaceRequest</c> and <c>RenameSpaceRequest</c> carry <c>Name</c> and nothing else:
    /// the new Space's id is server-generated, and a rename writes the resolved Space, never an id from the body.
    /// </summary>
    [Fact(Skip = "red: AC9 - CreateSpaceRequest and RenameSpaceRequest do not exist yet (story 3.1 Task 4)")]
    [Trait("Requirement", "AD-2")]
    [Trait("Requirement", "AR-34")]
    public void The_Space_request_contracts_carry_a_name_and_nothing_else()
    {
        foreach (var name in new[] { CreateSpaceRequestName, RenameSpaceRequestName })
        {
            var contract = Type.GetType(name);

            Assert.True(contract is not null, $"{name} is not declared.");
            Assert.Equal(["Name"], PropertyNames(contract));
            Assert.Equal(typeof(string), contract.GetProperty("Name")?.PropertyType);
        }
    }

    /// <summary>
    /// AC9, Q6 (a) assumed, Task 3 - <c>SpaceName.TryNormalise</c> trims and admits 1-200 characters, and nothing else
    /// (no uniqueness: AC5); its <c>MaxLength</c> is the column's, so there is one source of truth.
    /// </summary>
    [Fact(Skip = "red: AC9 - SpaceName does not exist yet (story 3.1 Task 3)")]
    [Trait("Requirement", "FR-6")]
    [Trait("Assumption", "FR-6-Q6")]
    public void The_Space_name_rule_admits_1_to_200_characters_and_the_column_agrees()
    {
        var spaceName = ProductionAssemblies.Domain.GetType(SpaceNameTypeName);

        Assert.True(spaceName is not null, $"{SpaceNameTypeName} is not declared.");

        var maxLength = ConstantOf(spaceName, "MaxLength");
        var column = ConstantOf(ProductionAssemblies.Infrastructure.GetType(SpaceConfigurationTypeName), "NameMaxLength");

        Assert.Equal(SpaceNameLimit, maxLength);
        Assert.Equal(maxLength, column);

        var normalise = spaceName.GetMethod("TryNormalise", BindingFlags.Public | BindingFlags.Static);

        Assert.True(normalise is not null, "SpaceName.TryNormalise(string?, out string) is not declared.");

        (string? Submitted, bool Admitted, string Name)[] edges =
        [
            (null, false, string.Empty),
            (string.Empty, false, string.Empty),
            ("   ", false, string.Empty),
            ("  Client work  ", true, "Client work"),
            ("x", true, "x"),
            (new string('s', SpaceNameLimit), true, new string('s', SpaceNameLimit)),
            (new string('s', SpaceNameLimit + 1), false, string.Empty),
            (" " + new string('s', SpaceNameLimit) + " ", true, new string('s', SpaceNameLimit)),
        ];

        foreach (var (submitted, admitted, name) in edges)
        {
            object?[] arguments = [submitted, null];

            Assert.Equal(admitted, normalise.Invoke(null, arguments));

            if (admitted)
            {
                Assert.Equal(name, arguments[1]);
            }
        }
    }

    /// <summary>
    /// AC6, AC7, AD-25, AR-3 - the Spaces-per-Account bound is declared by exactly one endpoint, the creation route
    /// (<c>POST /api/v1/spaces</c>), so the pipeline - not a slice - consults it there and nowhere else.
    /// </summary>
    [Fact(Skip = "red: AC6 - no endpoint consults Bound.SpacesPerAccount yet (story 3.1 Task 4)")]
    [Trait("Requirement", "AR-30")]
    [Trait("Requirement", "AD-25")]
    public void The_Spaces_per_Account_bound_is_consulted_by_the_creation_route_alone()
    {
        var consulting = RouteTable()
            .Where(endpoint => endpoint.Metadata.GetMetadata<RequiredBound>()?.Bound == Bound.SpacesPerAccount)
            .Select(endpoint => $"{string.Join(',', endpoint.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods ?? [])} {endpoint.RoutePattern.RawText}")
            .ToList();

        Assert.Equal([$"POST {Yello.Contracts.Spaces.SpaceRoutes.Directory}"], consulting);
    }

    /// <summary>
    /// AC10, FR-16, Task 5 - rename (<c>PATCH</c> on the active Space's route) declares exactly
    /// <c>RenameSpace</c>; creation (<c>POST</c> on the directory) declares no capability, because it is Account-scoped
    /// and no Role is held in a Space that does not exist yet (there is no <c>CreateSpace</c> capability).
    /// </summary>
    [Fact(Skip = "red: AC10 - POST and PATCH on /api/v1/spaces are not mapped yet (story 3.1 Tasks 4, 5)")]
    [Trait("Requirement", "FR-16")]
    [Trait("Requirement", "AR-11")]
    public void Rename_declares_RenameSpace_and_creation_declares_no_capability()
    {
        var endpoints = RouteTable();

        var rename = Assert.Single(endpoints, endpoint => endpoint.RoutePattern.RawText == Yello.Contracts.Spaces.SpaceRoutes.Active
            && endpoint.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods.Contains("PATCH") == true);
        var create = Assert.Single(endpoints, endpoint => endpoint.RoutePattern.RawText == Yello.Contracts.Spaces.SpaceRoutes.Directory
            && endpoint.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods.Contains("POST") == true);

        Assert.Equal([Capability.RenameSpace], rename.Metadata.OfType<RequiredCapability>().Select(required => required.Capability));
        Assert.Empty(create.Metadata.OfType<RequiredCapability>());
        Assert.DoesNotContain("CreateSpace", Enum.GetNames<Capability>());
    }

    /// <summary>
    /// The one public static <c>bool (string roleCode)</c> method in <c>Yello.Client</c> whose name contains
    /// <paramref name="named"/>.
    /// </summary>
    private static MethodInfo ClientRolePredicate(string named)
    {
        var predicate = ProductionAssemblies.Client.GetTypes()
            .SelectMany(type => type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static))
            .SingleOrDefault(method => method.Name.Contains(named, StringComparison.Ordinal)
                && method.ReturnType == typeof(bool)
                && method.GetParameters() is [{ ParameterType: var only }]
                && only == typeof(string));

        Assert.True(predicate is not null, $"No static bool (string roleCode) method named for {named} in Yello.Client.");

        return predicate;
    }

    /// <summary>
    /// An <c>int</c> constant (or static property) declared on <paramref name="type"/>, public or not.
    /// </summary>
    private static int? ConstantOf(Type? type, string name)
    {
        Assert.True(type is not null, $"The type declaring {name} is not found.");

        const BindingFlags any = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;

        return (type.GetField(name, any)?.GetValue(null) ?? type.GetProperty(name, any)?.GetValue(null)) as int?;
    }
}
