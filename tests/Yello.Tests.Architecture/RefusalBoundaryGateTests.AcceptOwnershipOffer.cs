using System.Globalization;
using System.Reflection;
using Microsoft.AspNetCore.Routing;
using Xunit;
using Yello.Domain.Memberships;
using Yello.Host.Spaces;

namespace Yello.Tests.Architecture;

/// <summary>
/// Structural gates for answering an Ownership Offer: the three identity-decided routes (the recipient's read, the
/// acceptance, the decline) and the read capability they declare; the allow-list of writes that may sit under that
/// capability; the acceptance's swap as two ordered <c>ExecuteUpdate</c> statements and never tracked entities; the
/// recipient's contract; and the handlers that can emit nothing.
/// </summary>
/// <remarks>
/// <para>
/// <b>Red-phase scaffolds.</b> Every gate here is skipped until its subject exists. Each finds its subject by name or by shape:
/// the route patterns, <c>Yello.Infrastructure.Persistence.OwnershipOfferStore.AcceptAsync</c>,
/// <c>Yello.Contracts.Spaces.OwnershipOfferForRecipientResponse</c>, and the handlers <c>AcceptOwnershipOfferHandler</c> and
/// <c>DeclineOwnershipOfferHandler</c>.
/// </para>
/// <para>
/// <b>Existing gates this work changes deliberately:</b> <c>The_leave_route_is_the_one_write_under_ReadProjectsAndTasks</c>
/// goes red the moment the acceptance is mapped, and is replaced by
/// <see cref="Only_the_identity_decided_writes_sit_under_ReadProjectsAndTasks"/> (Lee's Q1 (a)). <b>Kept green unchanged:</b>
/// <c>Only_the_offer_store_writes_an_OwnershipOffer_state</c> (its writer list stays exactly as it is), both Role-read gates,
/// the two <c>Assert.Single</c> gates over Application types named for "Authority", the capability vocabulary gates (no
/// <c>Capability</c> member is added) and <c>The_ownership_offer_problem_types_are_declared_and_each_has_client_copy</c>.
/// </para>
/// </remarks>
public sealed partial class RefusalBoundaryGateTests
{
    private const string MyOwnershipOfferPattern = "/api/v1/spaces/{spaceId:guid}/ownership-offers/mine";

    private const string OwnershipOfferAcceptancePattern = "/api/v1/spaces/{spaceId:guid}/ownership-offers/{offerId:guid}/acceptance";

    private const string OwnershipOfferDeclinePattern = "/api/v1/spaces/{spaceId:guid}/ownership-offers/{offerId:guid}/decline";

    private const string MembershipTypeName = "Yello.Domain.Memberships.Membership";

    private const string RoleTypeName = "Yello.Domain.Memberships.Role";

    /// <summary>
    /// The routes that answer an offer are exactly <c>GET</c> on the recipient's read and <c>POST</c> on the acceptance and the
    /// decline; each declares <c>ReadProjectsAndTasks</c>, which every Role holds, because identity - not Role - decides them
    /// inside the store; none is Session-only and none consults a bound, so a Token reaches all three.
    /// </summary>
    [Fact(Skip = "red: AC4, AC12 - the recipient's read, the acceptance and the decline are not mapped yet")]
    [Trait("Requirement", "FR-42")]
    [Trait("Requirement", "AR-13")]
    [Trait("Requirement", "AD-26")]
    [Trait("Requirement", "AD-4")]
    public void The_ownership_acceptance_routes_are_identity_decided_and_serve_both_surfaces()
    {
        var routes = RouteTable()
            .Where(endpoint => endpoint.RoutePattern.RawText is MyOwnershipOfferPattern or OwnershipOfferAcceptancePattern or OwnershipOfferDeclinePattern)
            .SelectMany(endpoint => (endpoint.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods ?? [])
                .Select(method => (Key: $"{method} {endpoint.RoutePattern.RawText}", Endpoint: endpoint)))
            .ToDictionary(route => route.Key, route => route.Endpoint, StringComparer.Ordinal);

        Assert.Equal(
            [$"GET {MyOwnershipOfferPattern}", $"POST {OwnershipOfferAcceptancePattern}", $"POST {OwnershipOfferDeclinePattern}"],
            routes.Keys.Order(StringComparer.Ordinal));
        Assert.All(routes.Values, endpoint => Assert.Equal(Capability.ReadProjectsAndTasks, endpoint.Metadata.GetMetadata<RequiredCapability>()?.Capability));
        Assert.All(routes.Values, endpoint => Assert.DoesNotContain(endpoint.Metadata, metadata => metadata.GetType().Name == "SessionOnlyEndpoint"));
        Assert.All(routes.Values, endpoint => Assert.Null(endpoint.Metadata.GetMetadata<RequiredBound>()));
    }

    /// <summary>
    /// The writes under <c>ReadProjectsAndTasks</c> are an explicit allow-list of identity-decided writes: leaving a Space,
    /// accepting an offer and declining one. Every other route declaring the read capability is a <c>GET</c>. Each of the three
    /// is guarded by identity in its own statement; a fourth write slipping under the read capability would hand every Viewer a
    /// write. Replaces <c>The_leave_route_is_the_one_write_under_ReadProjectsAndTasks</c> once the acceptance is mapped.
    /// </summary>
    [Fact(Skip = "red: AC4 (Q1 a) - the acceptance and the decline are not mapped yet; today the leave is the one write")]
    [Trait("Requirement", "FR-14")]
    [Trait("Requirement", "FR-42")]
    [Trait("Requirement", "AD-5")]
    [Trait("Requirement", "AD-20")]
    public void Only_the_identity_decided_writes_sit_under_ReadProjectsAndTasks()
    {
        var writes = RouteTable()
            .Where(endpoint => endpoint.Metadata.GetMetadata<RequiredCapability>()?.Capability == Capability.ReadProjectsAndTasks)
            .SelectMany(endpoint => (endpoint.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods ?? [])
                .Where(method => !string.Equals(method, "GET", StringComparison.Ordinal) && !string.Equals(method, "HEAD", StringComparison.Ordinal))
                .Select(method => $"{method} {endpoint.RoutePattern.RawText}"))
            .Order(StringComparer.Ordinal)
            .ToList();

        Assert.Equal(
            [$"DELETE {OwnMembershipPattern}", $"POST {OwnershipOfferAcceptancePattern}", $"POST {OwnershipOfferDeclinePattern}"],
            writes);
    }

    /// <summary>
    /// The acceptance changes Roles by two explicit, ordered <c>ExecuteUpdate</c> statements and never through tracked
    /// entities. Read from the IL of <c>OwnershipOfferStore.AcceptAsync</c>, its compiler-generated state machine, its
    /// lambdas, and every method of <c>Yello.Infrastructure</c> it calls, followed depth-first in call order: (i) at least two
    /// <c>ExecuteUpdateAsync</c> calls on <c>Membership</c>; (ii) no <c>SaveChanges</c>/<c>SaveChangesAsync</c>, no EF Core
    /// <c>Update</c>/<c>Attach</c> (or their <c>Range</c> forms) and no <c>Membership.set_Role</c>; (iii) the setter lambdas
    /// that set <c>Role</c>, in the order the accept path reaches them, set <c>Admin</c> and then <c>Owner</c> - the demote
    /// before the promote. Promote-before-demote transiently writes a second <c>Owner</c> row, which the filtered unique index
    /// refuses; demote-first leaves zero rows in that index, and zero never collides.
    /// </summary>
    /// <remarks>
    /// Check (iii) reads each setter lambda's value as the last <c>ldc.i4</c> before its <c>SetProperty&lt;Role&gt;</c> call,
    /// so it holds while the values are written as the enum constants (<c>Role.Admin</c>, <c>Role.Owner</c>). A value carried
    /// in a variable defeats it, and the gate then fails rather than passing; the swapped-order plant is also caught at
    /// runtime by the acceptance cases (<c>409 owner-role-fixed</c>).
    /// </remarks>
    [Fact(Skip = "red: AC5, AC6 - OwnershipOfferStore.AcceptAsync does not exist yet")]
    [Trait("Requirement", "AD-26")]
    [Trait("Requirement", "AR-13")]
    [Trait("Requirement", "AD-5")]
    public void The_ownership_acceptance_changes_Roles_by_two_ordered_ExecuteUpdate_calls_and_never_SaveChanges()
    {
        using var module = Mono.Cecil.ModuleDefinition.ReadModule(ProductionAssemblies.Infrastructure.Location);

        var store = module.GetType("Yello.Infrastructure.Persistence.OwnershipOfferStore");

        Assert.True(store is not null, "Yello.Infrastructure.Persistence.OwnershipOfferStore is not found.");

        var accept = store.Methods.SingleOrDefault(method => method.Name == "AcceptAsync");

        Assert.True(accept is not null, "OwnershipOfferStore declares no AcceptAsync.");

        var visited = new List<Mono.Cecil.MethodDefinition>();
        var roleSetters = new List<int>();

        WalkAcceptPath(module, accept, visited, roleSetters);

        var calls = visited
            .Where(method => method.HasBody)
            .SelectMany(method => method.Body.Instructions)
            .Select(instruction => instruction.Operand)
            .OfType<Mono.Cecil.MethodReference>()
            .ToList();

        var membershipUpdates = calls.Count(call =>
            call.Name == "ExecuteUpdateAsync"
            && call is Mono.Cecil.GenericInstanceMethod generic
            && generic.GenericArguments.Any(argument => argument.FullName == MembershipTypeName));

        Assert.True(membershipUpdates >= 2, $"The accept path calls ExecuteUpdateAsync on Membership {membershipUpdates} time(s); the swap is two statements.");

        var forbidden = calls
            .Where(WritesThroughTrackedEntities)
            .Select(call => $"{call.DeclaringType.FullName}.{call.Name}")
            .Distinct(StringComparer.Ordinal)
            .ToList();

        Assert.True(forbidden.Count == 0, "The accept path writes through tracked entities: " + string.Join(", ", forbidden));
        Assert.Equal([Convert.ToInt32(Role.Admin, CultureInfo.InvariantCulture), Convert.ToInt32(Role.Owner, CultureInfo.InvariantCulture)], roleSetters);
    }

    /// <summary>
    /// The recipient's answer carries exactly <c>Id</c>, <c>OffererName</c>, <c>ExpiresAt</c> and <c>ReadAt</c>: no recipient
    /// id, no address, no Role and no other Membership.
    /// </summary>
    [Fact(Skip = "red: AC1, AC12 - OwnershipOfferForRecipientResponse does not exist yet")]
    [Trait("Requirement", "AD-23")]
    [Trait("Requirement", "FR-42")]
    public void The_recipients_offer_contract_carries_exactly_its_four_members()
    {
        var response = ProductionAssemblies.Contracts.GetType("Yello.Contracts.Spaces.OwnershipOfferForRecipientResponse");

        Assert.True(response is not null, "OwnershipOfferForRecipientResponse is not declared in Yello.Contracts.Spaces.");
        Assert.Equal(["ExpiresAt", "Id", "OffererName", "ReadAt"], PropertyNames(response));
        Assert.Equal(typeof(Guid), response.GetProperty("Id")?.PropertyType);
        Assert.Equal(typeof(string), response.GetProperty("OffererName")?.PropertyType);
        Assert.Equal(typeof(DateTimeOffset), response.GetProperty("ExpiresAt")?.PropertyType);
        Assert.Equal(typeof(DateTimeOffset), response.GetProperty("ReadAt")?.PropertyType);
    }

    /// <summary>
    /// Nothing is emitted on acceptance or decline because neither can: the constructors of <c>AcceptOwnershipOfferHandler</c>
    /// and <c>DeclineOwnershipOfferHandler</c> name no outbox type. Anchor: each exists and takes an offer port. Sibling of
    /// <c>The_ownership_offer_revoke_and_lapse_take_no_outbox</c>. Plant 10 (an <c>IOutbox</c> parameter) turns it red.
    /// </summary>
    [Fact(Skip = "red: AC11 - the acceptance and decline handlers do not exist yet")]
    [Trait("Requirement", "FR-43")]
    [Trait("Requirement", "AR-33")]
    public void The_ownership_acceptance_and_decline_take_no_outbox()
    {
        foreach (var name in new[] { "AcceptOwnershipOfferHandler", "DeclineOwnershipOfferHandler" })
        {
            var handler = Assert.Single(ProductionAssemblies.Application.GetTypes(), type => type.Name == name);
            var parameters = handler.GetConstructors(BindingFlags.Public | BindingFlags.Instance).SelectMany(constructor => constructor.GetParameters()).ToList();

            Assert.Contains(parameters, parameter => parameter.ParameterType.Name.Contains("OwnershipOffer", StringComparison.Ordinal));
            Assert.DoesNotContain(parameters, parameter => parameter.ParameterType.Name.Contains("Outbox", StringComparison.Ordinal));
        }
    }

    /// <summary>
    /// Walks <paramref name="method"/> depth-first in IL order: an async method is read through its state machine's
    /// <c>MoveNext</c>; a delegate made from a method of this module is followed, and if it is a setter lambda that sets
    /// <c>Role</c> its value is appended to <paramref name="roleSetters"/>; a call to a method of this module is followed.
    /// </summary>
    private static void WalkAcceptPath(Mono.Cecil.ModuleDefinition module, Mono.Cecil.MethodDefinition method, List<Mono.Cecil.MethodDefinition> visited, List<int> roleSetters)
    {
        if (visited.Contains(method))
        {
            return;
        }

        visited.Add(method);

        var body = StateMachineMoveNext(method) ?? method;

        if (body != method)
        {
            visited.Add(body);
        }

        if (!body.HasBody)
        {
            return;
        }

        foreach (var instruction in body.Body.Instructions)
        {
            if (instruction.Operand is not Mono.Cecil.MethodReference called || called.DeclaringType.Scope != module)
            {
                continue;
            }

            var target = called.Resolve();

            if (target is null)
            {
                continue;
            }

            if (instruction.OpCode == Mono.Cecil.Cil.OpCodes.Ldftn && RoleSetterValue(target) is { } value && !visited.Contains(target))
            {
                visited.Add(target);
                roleSetters.Add(value);
                continue;
            }

            WalkAcceptPath(module, target, visited, roleSetters);
        }
    }

    /// <summary>
    /// The <c>MoveNext</c> of <paramref name="method"/>'s async state machine, or <c>null</c> when it is not async.
    /// </summary>
    private static Mono.Cecil.MethodDefinition? StateMachineMoveNext(Mono.Cecil.MethodDefinition method)
    {
        var attribute = method.CustomAttributes.FirstOrDefault(candidate => candidate.AttributeType.Name == nameof(System.Runtime.CompilerServices.AsyncStateMachineAttribute));

        return attribute?.ConstructorArguments[0].Value is Mono.Cecil.TypeReference machine
            ? machine.Resolve()?.Methods.SingleOrDefault(candidate => candidate.Name == "MoveNext")
            : null;
    }

    /// <summary>
    /// When <paramref name="method"/> calls <c>SetProperty&lt;Role&gt;</c>, the <c>Role</c> it sets: the last <c>ldc.i4</c>
    /// before the call. <c>null</c> otherwise.
    /// </summary>
    private static int? RoleSetterValue(Mono.Cecil.MethodDefinition method)
    {
        if (!method.HasBody)
        {
            return null;
        }

        int? last = null;

        foreach (var instruction in method.Body.Instructions)
        {
            if (LoadedInt(instruction) is { } loaded)
            {
                last = loaded;
            }

            if (instruction.Operand is Mono.Cecil.GenericInstanceMethod { Name: "SetProperty" } generic
                && generic.GenericArguments.Any(argument => argument.FullName == RoleTypeName))
            {
                return last;
            }
        }

        return null;
    }

    /// <summary>
    /// Whether <paramref name="call"/> saves or tracks: <c>SaveChanges</c> in any form, an EF Core <c>Update</c> or
    /// <c>Attach</c> (or their <c>Range</c> forms), or <c>Membership.set_Role</c>.
    /// </summary>
    private static bool WritesThroughTrackedEntities(Mono.Cecil.MethodReference call)
    {
        if (call.Name is "SaveChanges" or "SaveChangesAsync")
        {
            return true;
        }

        if (call.Name is "Update" or "UpdateRange" or "Attach" or "AttachRange")
        {
            return call.DeclaringType.Namespace.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal);
        }

        return call.Name == "set_Role" && call.DeclaringType.FullName == MembershipTypeName;
    }

    private static int? LoadedInt(Mono.Cecil.Cil.Instruction instruction)
    {
        var code = instruction.OpCode.Code;

        return code switch
        {
            Mono.Cecil.Cil.Code.Ldc_I4_0 => 0,
            Mono.Cecil.Cil.Code.Ldc_I4_1 => 1,
            Mono.Cecil.Cil.Code.Ldc_I4_2 => 2,
            Mono.Cecil.Cil.Code.Ldc_I4_3 => 3,
            Mono.Cecil.Cil.Code.Ldc_I4_S when instruction.Operand is sbyte small => small,
            Mono.Cecil.Cil.Code.Ldc_I4 when instruction.Operand is int value => value,
            _ => null,
        };
    }
}
