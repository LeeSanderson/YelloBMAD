using System.Reflection;
using System.Xml.Linq;
using Microsoft.AspNetCore.Routing;
using Xunit;
using Yello.Contracts;
using Yello.Domain.Memberships;
using Yello.Host.Spaces;

namespace Yello.Tests.Architecture;

/// <summary>
/// Story 4.2's structural gates for listing and revoking Invitations: the routes and their one capability on both surfaces
/// (AC1, AC2, Q1 (a), Q16 (a)), the list row's contract (AC1, Q5 (a)), the one writer of <c>Invitation.State</c> (AC5,
/// AC11, A-10, Q13 (a)), the revoke handler's missing outbox (AC7), the slices' blindness to the issuer (AC2, AC3, AR-3),
/// and the new problem type with its copy (AC4, AC9). Written by the story 4.2 ATDD run, 2026-11-01.
/// </summary>
/// <remarks>
/// <para>
/// <b>Written as red-phase scaffolds, activated by the dev phase.</b> Each gate finds its subject by name (route pattern,
/// reflection, Cecil or the resource file), so a renamed subject turns it red. The names are the story's Project
/// Structure Notes: <c>Yello.Contracts.Invitations.InvitationResponse</c>, <c>ListInvitationsHandler</c> and
/// <c>RevokeInvitationHandler</c> in <c>Yello.Application</c>, <c>Yello.Infrastructure.Persistence.InvitationStore</c>. A
/// different name is corrected here first and recorded in the Debug Log. The route patterns stay literals, as in
/// <c>RefusalBoundaryGateTests.Labels.cs</c>: they are what the gate asserts.
/// </para>
/// <para>
/// <b>Kept beside, not inside, <c>RefusalBoundaryGateTests.Invitations.cs</c></b> (the story names that file): the same
/// partial class, in a file of its own so 4.1's gates read as 4.1 wrote them. The 4.2 problem-type gate is a sibling of
/// 4.1's <c>The_invitation_problem_types_are_declared_and_each_has_client_copy_naming_story_4_1</c>, not a generalisation of
/// it (the story's open choice, taken here).
/// </para>
/// <para>
/// <b>Left active and green, by design:</b> <c>Only_the_shared_actionable_predicate_compares_ExpiresAt</c> (gate (i)) and
/// <c>No_production_code_runs_on_a_timer_or_as_a_hosted_service</c> - AC11 requires both to stay green unchanged.
/// </para>
/// </remarks>
public sealed partial class RefusalBoundaryGateTests
{
    private const string InvitationsPattern = "/api/v1/spaces/{spaceId:guid}/invitations";

    private const string InvitationPattern = "/api/v1/spaces/{spaceId:guid}/invitations/{invitationId:guid}";

    /// <summary>
    /// AC1, AC2, AD-4, Q1 (a), Q16 (a) [SECURITY] - the Invitation routes are exactly <c>GET</c> and <c>POST</c> on the
    /// collection and <c>DELETE</c> on the item; all three require <c>IssueAndRevokeInvitations</c> (the list carries
    /// addresses, section 6.1 and AD-23; never <c>ReadProjectsAndTasks</c>), none is Session-only and none consults a bound.
    /// Sibling of <c>The_Label_routes_declare_their_capability_serve_both_surfaces_and_consult_no_bound</c>. Plants 6 and 7:
    /// the list under <c>ReadProjectsAndTasks</c>, or the revoke without <c>RequiresCapability</c>.
    /// </summary>
    [Fact]
    [Trait("Requirement", "FR-12")]
    [Trait("Requirement", "AD-4")]
    [Trait("Requirement", "AD-23")]
    public void The_Invitation_routes_all_require_IssueAndRevokeInvitations_and_serve_both_surfaces()
    {
        var routes = RouteTable()
            .Where(endpoint => endpoint.RoutePattern.RawText is InvitationsPattern or InvitationPattern)
            .SelectMany(endpoint => (endpoint.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods ?? [])
                .Select(method => (Key: $"{method} {endpoint.RoutePattern.RawText}", Endpoint: endpoint)))
            .ToDictionary(route => route.Key, route => route.Endpoint, StringComparer.Ordinal);

        Assert.Equal(
            [$"DELETE {InvitationPattern}", $"GET {InvitationsPattern}", $"POST {InvitationsPattern}"],
            routes.Keys.Order(StringComparer.Ordinal));
        Assert.All(routes.Values, endpoint => Assert.Equal(Capability.IssueAndRevokeInvitations, endpoint.Metadata.GetMetadata<RequiredCapability>()?.Capability));
        Assert.All(routes.Values, endpoint => Assert.DoesNotContain(endpoint.Metadata, metadata => metadata.GetType().Name == "SessionOnlyEndpoint"));
        Assert.All(routes.Values, endpoint => Assert.Null(endpoint.Metadata.GetMetadata<RequiredBound>()));
    }

    /// <summary>
    /// AC1, Q5 (a), NFR-6 - the list row's contract, <c>InvitationResponse</c>, carries exactly <c>Id</c> (a Guid),
    /// <c>EmailAddress</c>, <c>Role</c> and <c>IssuerName</c> (strings) and <c>IssuedAt</c> (a DateTimeOffset): never the
    /// token hash, the normalised address, the issuer's Account, the Space, the state or the expiry. Plant 8: a sixth
    /// property.
    /// </summary>
    [Fact]
    [Trait("Requirement", "FR-12")]
    [Trait("Requirement", "NFR-6")]
    [Trait("Requirement", "AD-23")]
    public void The_Invitation_list_response_carries_the_id_address_Role_issuer_and_date_and_nothing_else()
    {
        var response = ProductionAssemblies.Contracts.GetType("Yello.Contracts.Invitations.InvitationResponse");

        Assert.True(response is not null, "Yello.Contracts.Invitations.InvitationResponse is not declared.");
        Assert.Equal(["EmailAddress", "Id", "IssuedAt", "IssuerName", "Role"], PropertyNames(response));
        Assert.Equal(typeof(Guid), response.GetProperty("Id")?.PropertyType);
        Assert.Equal(typeof(DateTimeOffset), response.GetProperty("IssuedAt")?.PropertyType);

        foreach (var name in new[] { "EmailAddress", "Role", "IssuerName" })
        {
            Assert.Equal(typeof(string), response.GetProperty(name)?.PropertyType);
        }
    }

    /// <summary>
    /// AC5, AC11, A-10, AD-27, Q13 (a) - <c>InvitationStore</c> is the only production writer of <c>Invitation.State</c>:
    /// the only bulk write generic over <c>Invitation</c> is its <c>ExecuteUpdateAsync</c> (the revoke - the anchor), no
    /// <c>ExecuteDelete</c> on <c>Invitation</c> exists anywhere, and only it calls <c>Invitation.set_State</c> (the issue's
    /// object initializer, which stores <c>Pending</c> - the other sanctioned write). Read from the compiled IL, as
    /// <c>The_only_bulk_writes_of_a_Space_are_the_rename_and_the_deletion_in_SpaceStore</c> is. 4.3 adds its accept here.
    /// Plants 4 and 5: the revoke as <c>ExecuteDelete</c>, or a second <c>State</c> writer in another type.
    /// </summary>
    [Fact]
    [Trait("Requirement", "AR-31")]
    [Trait("Requirement", "AD-27")]
    [Trait("Requirement", "FR-10")]
    public void Only_the_Invitation_store_writes_an_Invitation_state()
    {
        string[] bulk = ["ExecuteUpdate", "ExecuteUpdateAsync", "ExecuteDelete", "ExecuteDeleteAsync"];

        var calls = InvitationCalls();
        var bulkWrites = new SortedSet<string>(
            calls
                .Where(call => bulk.Contains(call.Name, StringComparer.Ordinal) && call.GenericArguments.Contains(InvitationTypeName, StringComparer.Ordinal))
                .Select(call => $"{call.Caller}.{call.Name}"),
            StringComparer.Ordinal);
        var stateSetters = new SortedSet<string>(
            calls
                .Where(call => call.Name == "set_State" && call.DeclaringType == InvitationTypeName)
                .Select(call => call.Caller),
            StringComparer.Ordinal);

        Assert.Equal(["Yello.Infrastructure.Persistence.InvitationStore.ExecuteUpdateAsync"], bulkWrites);
        Assert.Contains("Yello.Infrastructure.Persistence.InvitationStore", stateSetters);
        Assert.Subset(new SortedSet<string>(["Yello.Infrastructure.Persistence.InvitationStore"], StringComparer.Ordinal), stateSetters);
    }

    /// <summary>
    /// Every production call, as the outermost calling type, the method's name, its declaring type and its generic
    /// arguments - read while the module is open, the raw material of the state-writer gate.
    /// </summary>
    private static List<(string Caller, string Name, string DeclaringType, string[] GenericArguments)> InvitationCalls()
    {
        var calls = new List<(string, string, string, string[])>();

        foreach (var assembly in RuntimeAssemblies())
        {
            using var module = Mono.Cecil.ModuleDefinition.ReadModule(assembly.Location);

            calls.AddRange(module.GetTypes()
                .SelectMany(type => type.Methods
                    .Where(method => method.HasBody)
                    .SelectMany(method => method.Body.Instructions)
                    .Select(instruction => instruction.Operand)
                    .OfType<Mono.Cecil.MethodReference>()
                    .Select(called => (
                        OutermostName(type),
                        called.Name,
                        called.DeclaringType.FullName,
                        called is Mono.Cecil.GenericInstanceMethod generic ? generic.GenericArguments.Select(argument => argument.FullName).ToArray() : Array.Empty<string>()))));
        }

        return calls;
    }

    /// <summary>
    /// AC7, FR-39 ("exactly one email per Invitation"), AD-26's notification rule - the revoke enqueues nothing because it
    /// cannot: <c>RevokeInvitationHandler</c>'s constructors name no outbox type. Anchor: the handler exists and takes an
    /// Invitation port. Plant 9: an <c>IOutbox</c> parameter.
    /// </summary>
    [Fact]
    [Trait("Requirement", "FR-39")]
    [Trait("Requirement", "AR-33")]
    public void The_revoke_handler_takes_no_outbox()
    {
        var handler = Assert.Single(ProductionAssemblies.Application.GetTypes(), type => type.Name == "RevokeInvitationHandler");
        var parameters = handler.GetConstructors(BindingFlags.Public | BindingFlags.Instance).SelectMany(constructor => constructor.GetParameters()).ToList();

        Assert.Contains(parameters, parameter => parameter.ParameterType.Name.Contains("Invitation", StringComparison.Ordinal));
        Assert.DoesNotContain(parameters, parameter => parameter.ParameterType.Name.Contains("Outbox", StringComparison.Ordinal));
    }

    /// <summary>
    /// AC2, AC3, AR-3, FR-12 - nothing in the list or the revoke depends on the issuer: neither slice handler nor the
    /// Invitation endpoints read <c>Invitation.IssuedByAccountId</c> (the handler never compares the caller with the issuer;
    /// the list shows the name captured at issue). Anchor: both handlers exist. Plant 11: a revoke filtered on
    /// <c>IssuedByAccountId == caller</c> in a handler.
    /// </summary>
    [Fact]
    [Trait("Requirement", "FR-12")]
    [Trait("Requirement", "AR-3")]
    [Trait("Requirement", "AD-5")]
    public void Neither_the_list_nor_the_revoke_reads_the_issuers_Account()
    {
        string[] slices = ["ListInvitationsHandler", "RevokeInvitationHandler"];

        foreach (var slice in slices)
        {
            Assert.Single(ProductionAssemblies.Application.GetTypes(), type => type.Name == slice);
        }

        var readers = RoleReaders(InvitationTypeName, "get_IssuedByAccountId");

        Assert.DoesNotContain(readers, reader => slices.Contains(reader.Split('.')[^1], StringComparer.Ordinal) || reader.EndsWith(".InvitationEndpoints", StringComparison.Ordinal));
    }

    /// <summary>
    /// AC4, AC9, AR-34, Q2 (a) - <c>invitation-not-pending</c> is declared in <c>ProblemTypes</c> and has client copy of its
    /// own in <c>ClientCopy.resx</c> whose comment names story 4.2. Sibling of 4.1's problem-type gate.
    /// </summary>
    [Fact]
    [Trait("Requirement", "AR-34")]
    [Trait("Requirement", "FR-12")]
    public void The_invitation_not_pending_problem_type_is_declared_and_has_client_copy_naming_story_4_2()
    {
        const string code = "invitation-not-pending";

        var declared = typeof(ProblemTypes)
            .GetProperties(BindingFlags.Public | BindingFlags.Static)
            .Select(property => property.GetValue(null) as string)
            .ToHashSet(StringComparer.Ordinal);

        var copy = XDocument.Load(Path.Combine(RepositoryLayout.Root.FullName, "Yello.Client", "Localisation", "ClientCopy.resx"))
            .Root!
            .Elements("data")
            .ToDictionary(data => (string)data.Attribute("name")!, data => (string?)data.Element("comment") ?? string.Empty, StringComparer.Ordinal);

        Assert.Contains("urn:yello:problem:" + code, declared);
        Assert.True(copy.TryGetValue(code, out var comment), $"ClientCopy.resx has no entry for {code}.");
        Assert.Contains("4.2", comment, StringComparison.Ordinal);
    }
}
