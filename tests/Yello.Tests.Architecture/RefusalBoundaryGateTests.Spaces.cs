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
/// <b>Written as red-phase scaffolds by the story 3.1 ATDD run, activated by story 3.1.</b> The contracts and the name
/// rule are typed references now, so a rename is a compile error; the client predicates are still found by name,
/// because the client assembly's static classes are what the gate scans, and the internal column configuration by name.
/// </para>
/// <para>
/// <b>Active gates amended by story 3.1 (Task 9)</b>, each for exactly one named exception:
/// <c>No_endpoint_without_spaceId_in_its_path_reaches_a_Space_scoped_entity</c> (creation is the second named
/// exemption beside registration), <c>Every_Account_scoped_endpoint_names_an_AD_24_surface</c> and
/// <c>SpaceSwitcherGateTests.The_Account_scoped_surfaces_are_exactly_the_AD_24_names_as_amended</c> (the
/// <c>SpaceCreation</c> surface), <c>SpaceSwitcherGateTests.No_caller_but_the_Space_creation_unit_of_work_sets_both_session_context_keys</c>,
/// <c>Space_is_read_only_where_named</c> (the provisioning and the Space store), and
/// <c>The_endpoints_unreachable_with_a_Token_are_Token_management_the_switcher_and_the_move</c> (creation, on the
/// switcher's path).
/// </para>
/// </remarks>
public sealed partial class RefusalBoundaryGateTests
{
    private const string SpaceConfigurationTypeName = "Yello.Infrastructure.Persistence.Configurations.SpaceConfiguration";

    /// <summary>
    /// Story 3.1 Q6 (a): the longest provisioned name is 128 + "'s Space", so 200.
    /// </summary>
    private const int SpaceNameLimit = 200;

    /// <summary>
    /// AC3, AC4, UX-DR27, Task 7 - the client's predicate for offering rename agrees with the capability matrix for
    /// every Role: it offers it exactly where <c>RenameSpace</c> is not denied (Owner, Admin). Sibling of
    /// <c>The_clients_Label_definition_predicate_agrees_with_the_capability_matrix</c>.
    /// </summary>
    [Fact]
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
    [Fact]
    [Trait("Requirement", "UX-DR27")]
    [Trait("Requirement", "UX-DR9")]
    public void The_clients_Space_settings_predicate_is_the_union_of_its_sections()
    {
        var predicate = ClientRolePredicate("SpaceSettings");

        foreach (var role in Enum.GetValues<Role>())
        {
            var expected = RoleCapabilityMatrix.Decide(role, Capability.RenameSpace) != CapabilityDecision.Denied
                || RoleCapabilityMatrix.Decide(role, Capability.ManageLabels) != CapabilityDecision.Denied
                || RoleCapabilityMatrix.Decide(role, Capability.DeleteSpace) != CapabilityDecision.Denied;

            Assert.True(
                predicate.Invoke(null, [StableCode.Of(role)]) is bool shown && shown == expected,
                $"The client's Space settings predicate disagrees with its sections for {role}.");
        }
    }

    /// <summary>
    /// AC9, AD-2, Task 4 - <c>CreateSpaceRequest</c> and <c>RenameSpaceRequest</c> carry <c>Name</c> and nothing else:
    /// the new Space's id is server-generated, and a rename writes the resolved Space, never an id from the body.
    /// </summary>
    [Fact]
    [Trait("Requirement", "AD-2")]
    [Trait("Requirement", "AR-34")]
    public void The_Space_request_contracts_carry_a_name_and_nothing_else()
    {
        foreach (var contract in new[] { typeof(Yello.Contracts.Spaces.CreateSpaceRequest), typeof(Yello.Contracts.Spaces.RenameSpaceRequest) })
        {
            Assert.Equal(["Name"], PropertyNames(contract));
            Assert.Equal(typeof(string), contract.GetProperty("Name")?.PropertyType);
        }
    }

    /// <summary>
    /// AC9, Q6 (a) assumed, Task 3 - <c>SpaceName.TryNormalise</c> trims and admits 1-200 characters, and nothing else
    /// (no uniqueness: AC5); its <c>MaxLength</c> is the column's, so there is one source of truth.
    /// </summary>
    [Fact]
    [Trait("Requirement", "FR-6")]
    [Trait("Assumption", "FR-6-Q6")]
    public void The_Space_name_rule_admits_1_to_200_characters_and_the_column_agrees()
    {
        var spaceName = typeof(Yello.Domain.Spaces.SpaceName);

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
    [Fact]
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
    [Fact]
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
    /// AC3, AC10, Q5 (a), Task 9 - <c>Space</c> carries no row-level security policy, so the only thing that scopes a write
    /// to it is the statement's own filter. Exactly one production type updates or deletes a <c>Space</c> in bulk:
    /// <c>SpaceStore</c> - its rename and, since the AD-24 amendment of 2026-10-31 (story 3.3), its deletion. Read from the compiled IL: a generic <c>ExecuteUpdate</c>/<c>ExecuteDelete</c>
    /// whose source type is <c>Space</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>What it does not hold.</b> It names the one bulk writer; it does not read that writer's filter, which is held
    /// by review and by the rename slice cases (<c>WHERE Id =</c> the resolved context's <c>SpaceId</c>). It scans no
    /// tracked write: an <c>Update</c> or <c>Remove</c> of a tracked <c>Space</c> entity is held by
    /// <c>Space_is_read_only_where_named</c>, which names every type that reaches <c>Space</c> at all.
    /// </para>
    /// <para>
    /// <b>Vacuity guard:</b> the scan finds <c>ExecuteUpdateAsync</c> calls on other entities (a Project rename), so it
    /// reads what it claims to. Planted by story 3.1 with a second <c>ExecuteUpdateAsync</c> on <c>Space</c>, and caught.
    /// </para>
    /// </remarks>
    [Fact]
    [Trait("Requirement", "AR-29")]
    [Trait("Requirement", "NFR-1")]
    public void The_only_update_of_a_Space_is_the_rename_of_the_resolved_Space()
    {
        const string space = "Yello.Domain.Spaces.Space";

        var calls = BulkWriteCalls();

        Assert.Contains(calls, call => call.Caller == "Yello.Infrastructure.Persistence.ProjectStore");

        var bulkWriters = calls
            .Where(call => call.SourceTypes.Contains(space, StringComparer.Ordinal))
            .Select(call => call.Caller)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToList();

        Assert.True(
            bulkWriters.SequenceEqual(["Yello.Infrastructure.Persistence.SpaceStore"], StringComparer.Ordinal),
            "Space carries no policy (Q5 (a), the AD-24 amendment), so its only bulk writes are the rename or the deletion "
            + "of the resolved Space, in SpaceStore (the AD-24 amendment of 2026-10-31, story 3.3). Found: "
            + string.Join(", ", bulkWriters));
    }

    /// <summary>
    /// Q6 (a), Tasks 6, 7 - the client's courtesy check of a Space name (<c>SpaceNameRule.IsSpaceName</c>) agrees with the
    /// Domain's <c>SpaceName</c> at every edge: empty, blank, one character, trimmed, 200 and 201 characters. The sibling of
    /// <c>The_clients_Label_name_predicate_agrees_with_the_Domain_rule</c>.
    /// </summary>
    [Fact]
    [Trait("Requirement", "FR-6")]
    [Trait("Assumption", "FR-6-Q6")]
    public void The_clients_Space_name_predicate_agrees_with_the_Domain_rule()
    {
        var predicate = ProductionAssemblies.Client.GetTypes()
            .SelectMany(type => type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static))
            .SingleOrDefault(method => method.Name == "IsSpaceName"
                && method.ReturnType == typeof(bool)
                && method.GetParameters() is [{ ParameterType: var only }]
                && only == typeof(string));

        Assert.True(predicate is not null, "No static bool IsSpaceName(string) in Yello.Client.");

        var longest = Yello.Domain.Spaces.SpaceName.MaxLength;

        string[] inputs =
        [
            string.Empty,
            "   ",
            "a",
            "  a  ",
            new string('n', longest),
            new string('n', longest + 1),
            "  " + new string('n', longest) + "  ",
        ];

        foreach (var input in inputs)
        {
            var expected = Yello.Domain.Spaces.SpaceName.TryNormalise(input, out _);

            Assert.True(
                predicate.Invoke(null, [input]) is bool shown && shown == expected,
                $"The client's Space name check disagrees with the Domain for a {input.Length}-character input.");
        }
    }

    /// <summary>
    /// AC14, AR-34, I-17, Task 9 - no source-generated log message in the product takes a name as an argument: a Space's
    /// id may be logged (story 3.1's creation event carries it), its name never - nor any other name a person typed.
    /// Static half of <c>ActiveSpaceEndpointTests.No_Space_name_reaches_the_log_on_creation_or_rename</c>.
    /// </summary>
    /// <remarks>
    /// <b>Vacuity guard:</b> the scan finds story 3.1's <c>SpaceCreated</c> message and its <c>spaceId</c>. Planted by
    /// story 3.1 with a <c>spaceName</c> argument on that message, and caught.
    /// </remarks>
    [Fact]
    [Trait("Requirement", "AR-34")]
    [Trait("Requirement", "NFR-6")]
    public void No_log_message_takes_a_name()
    {
        // Names the deployment gives, not a person: the Data Protection application name and the database resource name.
        string[] systemNames = ["applicationName", "resourceName"];

        var messages = ProductionAssemblies.All
            .Where(assembly => assembly != ProductionAssemblies.AppHost)
            .SelectMany(assembly => assembly.GetTypes())
            .SelectMany(type => type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            .Where(method => method.GetCustomAttributes().Any(attribute => attribute.GetType().Name == "LoggerMessageAttribute"))
            .ToList();

        var created = Assert.Single(messages, method => method.Name == "SpaceCreated");
        Assert.Contains(created.GetParameters(), parameter => parameter.Name == "spaceId");

        var offenders = messages
            .SelectMany(method => method.GetParameters()
                .Where(parameter => parameter.Name?.Contains("name", StringComparison.OrdinalIgnoreCase) == true
                    && !systemNames.Contains(parameter.Name, StringComparer.Ordinal))
                .Select(parameter => $"{method.DeclaringType?.Name}.{method.Name}({parameter.Name})"))
            .ToList();

        Assert.True(offenders.Count == 0, "A log message takes a name, which a person typed (AR-34): " + string.Join(", ", offenders));
    }

    /// <summary>
    /// Every production call to EF Core's bulk <c>ExecuteUpdate</c>/<c>ExecuteDelete</c>, as the outermost calling type and
    /// the call's generic arguments.
    /// </summary>
    private static List<(string Caller, string[] SourceTypes)> BulkWriteCalls()
    {
        string[] bulk = ["ExecuteUpdate", "ExecuteUpdateAsync", "ExecuteDelete", "ExecuteDeleteAsync"];

        var calls = new List<(string, string[])>();

        foreach (var assembly in ProductionAssemblies.All.Where(assembly => assembly != ProductionAssemblies.AppHost))
        {
            using var module = Mono.Cecil.ModuleDefinition.ReadModule(assembly.Location);

            calls.AddRange(module.GetTypes()
                .SelectMany(type => type.Methods
                    .Where(method => method.HasBody)
                    .SelectMany(method => method.Body.Instructions)
                    .Select(instruction => instruction.Operand)
                    .OfType<Mono.Cecil.GenericInstanceMethod>()
                    .Where(called => bulk.Contains(called.Name, StringComparer.Ordinal))
                    .Select(called => (OutermostName(type), called.GenericArguments.Select(argument => argument.FullName).ToArray()))));
        }

        return calls;
    }

    private static string OutermostName(Mono.Cecil.TypeDefinition type) =>
        type.DeclaringType is null ? type.FullName : OutermostName(type.DeclaringType);

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
