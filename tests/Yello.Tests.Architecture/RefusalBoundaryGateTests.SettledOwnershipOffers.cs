using System.Globalization;
using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Routing;
using Xunit;
using Yello.Domain.Memberships;
using Yello.Host.Spaces;
using Cecil = Mono.Cecil;

namespace Yello.Tests.Architecture;

/// <summary>
/// Structural gates for the states an Ownership Offer leaves <c>Pending</c> for: the Owner's declined read (its route, its
/// capability, its contract and its handler), the revoke's outcome for a caller whose Membership ended while it waited, every
/// offer transition guarded by the shared actionable predicate, and A-10 for both offers and Invitations - no scheduled
/// component reaches a write of either state, and expiry is never the predicate of a write but for the one named exception.
/// </summary>
/// <remarks>
/// <para>
/// <b>Each gate finds its subject by name or by shape</b>: the route pattern; <c>Yello.Contracts.Spaces.DeclinedOwnershipOfferResponse</c>;
/// the handler <c>ReadDeclinedOwnershipOfferHandler</c>; the enum <c>OwnershipOfferRevocation</c>;
/// <c>Yello.Infrastructure.Persistence.OwnershipOfferStore</c>, <c>OwnershipOfferWrites</c> and <c>InvitationStore</c>; and
/// <c>OfferPredicates.OwnershipOfferActionable</c> and <c>OwnershipOfferPendingButExpired</c>.
/// </para>
/// <para>
/// <b>Methods are compared at source granularity.</b> An async method's state machine, its lambdas and its local functions are
/// attributed to the method that declares them, so "this method writes" and "this method composes the predicate" read the
/// method as written, not the compiler's split of it.
/// </para>
/// <para>
/// <b>Existing gates the declined read widens deliberately:</b> <c>OfferOwnershipTransfer_is_required_by_exactly_the_three_offer_routes</c>
/// is replaced by <see cref="OfferOwnershipTransfer_is_required_by_exactly_the_four_offer_routes"/>, and
/// <c>The_ownership_offer_routes_require_OfferOwnershipTransfer_and_serve_both_surfaces</c> gains the pattern. <b>Kept green
/// unchanged:</b> <c>Only_the_offer_store_writes_an_OwnershipOffer_state</c>,
/// <c>No_production_code_runs_on_a_timer_or_as_a_hosted_service</c>, both no-outbox gates, and the problem-type gate (no new
/// problem type).
/// </para>
/// </remarks>
public sealed partial class RefusalBoundaryGateTests
{
    private const string DeclinedOwnershipOfferPattern = "/api/v1/spaces/{spaceId:guid}/ownership-offers/declined";

    private const string OwnershipOfferStoreName = "Yello.Infrastructure.Persistence.OwnershipOfferStore";

    private const string OwnershipOfferWritesName = "Yello.Infrastructure.Persistence.OwnershipOfferWrites";

    private const string InvitationStoreName = "Yello.Infrastructure.Persistence.InvitationStore";

    private const string OwnershipOfferStateTypeName = "Yello.Domain.Spaces.OwnershipOfferState";

    private const string DeclinedReadPending = "red: AC1 - the Owner's declined read (Q1 (a)) is not mapped yet";

    private static readonly string[] OfferBulkWrites = ["ExecuteUpdate", "ExecuteUpdateAsync", "ExecuteDelete", "ExecuteDeleteAsync"];

    /// <summary>
    /// The matrix's "Offer to transfer ownership" row governs exactly the four offer routes: the offer, the pending read, the
    /// declined read and the revoke. No other route borrows it, and none of the four is folded under another capability.
    /// Replaces the three-route gate when the declined read is mapped.
    /// </summary>
    [Fact(Skip = DeclinedReadPending + "; this gate replaces OfferOwnershipTransfer_is_required_by_exactly_the_three_offer_routes")]
    [Trait("Requirement", "FR-42")]
    [Trait("Requirement", "AD-1")]
    public void OfferOwnershipTransfer_is_required_by_exactly_the_four_offer_routes()
    {
        var requiring = RouteTable()
            .Where(endpoint => endpoint.Metadata.GetMetadata<RequiredCapability>()?.Capability == Capability.OfferOwnershipTransfer)
            .SelectMany(endpoint => (endpoint.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods ?? [])
                .Select(method => $"{method} {endpoint.RoutePattern.RawText}"))
            .Order(StringComparer.Ordinal)
            .ToList();

        Assert.Equal(
            [$"DELETE {OwnershipOfferPattern}", $"GET {DeclinedOwnershipOfferPattern}", $"GET {PendingOwnershipOfferPattern}", $"POST {OwnershipOffersPattern}"],
            requiring);
    }

    /// <summary>
    /// The declined read is exactly one <c>GET</c>; it requires <c>OfferOwnershipTransfer</c>, is not Session-only and consults
    /// no bound, so a Token reaches it.
    /// </summary>
    [Fact(Skip = DeclinedReadPending)]
    [Trait("Requirement", "FR-42")]
    [Trait("Requirement", "AD-4")]
    [Trait("Requirement", "AD-1")]
    public void The_declined_ownership_offer_read_requires_OfferOwnershipTransfer_and_serves_both_surfaces()
    {
        var endpoint = Assert.Single(RouteTable(), candidate => candidate.RoutePattern.RawText == DeclinedOwnershipOfferPattern);

        Assert.Equal(["GET"], endpoint.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods ?? []);
        Assert.Equal(Capability.OfferOwnershipTransfer, endpoint.Metadata.GetMetadata<RequiredCapability>()?.Capability);
        Assert.DoesNotContain(endpoint.Metadata, metadata => metadata.GetType().Name == "SessionOnlyEndpoint");
        Assert.Null(endpoint.Metadata.GetMetadata<RequiredBound>());
    }

    /// <summary>
    /// The declined answer carries exactly <c>Id</c> (a <c>Guid</c>) and <c>RecipientName</c> (a nullable <c>string</c>, null
    /// once the Membership is gone): no address, no recipient id, no Role.
    /// </summary>
    [Fact(Skip = DeclinedReadPending)]
    [Trait("Requirement", "AD-23")]
    [Trait("Requirement", "FR-42")]
    public void The_declined_ownership_offer_contract_carries_exactly_Id_and_RecipientName()
    {
        var response = ProductionAssemblies.Contracts.GetType("Yello.Contracts.Spaces.DeclinedOwnershipOfferResponse");

        Assert.True(response is not null, "DeclinedOwnershipOfferResponse is not declared in Yello.Contracts.Spaces.");
        Assert.Equal(["Id", "RecipientName"], PropertyNames(response));
        Assert.Equal(typeof(Guid), response.GetProperty("Id")?.PropertyType);

        var name = response.GetProperty("RecipientName");

        Assert.NotNull(name);
        Assert.Equal(typeof(string), name.PropertyType);
        Assert.Equal(NullabilityState.Nullable, new NullabilityInfoContext().Create(name).ReadState);
    }

    /// <summary>
    /// The declined read can emit nothing and moves nothing: <c>ReadDeclinedOwnershipOfferHandler</c>'s constructors take an
    /// offer port and name no outbox type. Sibling of <c>The_ownership_acceptance_and_decline_take_no_outbox</c>.
    /// </summary>
    [Fact(Skip = DeclinedReadPending)]
    [Trait("Requirement", "FR-43")]
    [Trait("Requirement", "AR-33")]
    public void The_declined_ownership_offer_read_takes_no_outbox()
    {
        var handler = Assert.Single(ProductionAssemblies.Application.GetTypes(), type => type.Name == "ReadDeclinedOwnershipOfferHandler");
        var parameters = handler.GetConstructors(BindingFlags.Public | BindingFlags.Instance).SelectMany(constructor => constructor.GetParameters()).ToList();

        Assert.Contains(parameters, parameter => parameter.ParameterType.Name.Contains("OwnershipOffer", StringComparison.Ordinal));
        Assert.DoesNotContain(parameters, parameter => parameter.ParameterType.Name.Contains("Outbox", StringComparison.Ordinal));
    }

    /// <summary>
    /// The revoke names the outcome of a caller whose Membership ended while the revoke waited on the lock:
    /// <c>OwnershipOfferRevocation.LostMembership</c>, which the endpoint answers with the recorded boundary <c>404</c>, beside
    /// <c>Revoked</c>, <c>NotPending</c>, <c>NotFound</c> and <c>NoLongerOwner</c>. The runtime proof is the revoke race.
    /// </summary>
    [Fact(Skip = "red: AC4 - OwnershipOfferRevocation has no LostMembership member yet")]
    [Trait("Requirement", "AD-26")]
    [Trait("Requirement", "AD-3")]
    public void The_revoke_outcome_names_a_lost_Membership()
    {
        var outcome = Assert.Single(ProductionAssemblies.Application.GetTypes(), type => type.Name == "OwnershipOfferRevocation");

        Assert.True(outcome.IsEnum, "OwnershipOfferRevocation is not an enum.");
        Assert.Equal(["LostMembership", "NoLongerOwner", "NotFound", "NotPending", "Revoked"], Enum.GetNames(outcome).Order(StringComparer.Ordinal));
    }

    /// <summary>
    /// Every transition guarded by <c>State = Pending</c> and a rowcount, not by the <c>Idempotency-Key</c> alone: each method of
    /// <c>OwnershipOfferStore</c> that bulk-updates an offer to <c>Accepted</c>, <c>Declined</c> or <c>Revoked</c> also composes
    /// <c>OfferPredicates.OwnershipOfferActionable</c>, read from its IL with its state machine and lambdas. The key deduplicates
    /// one request; the guarded statement refuses a second request under a new key, so no route admits ownership arriving
    /// unrequested. The two writers of <c>Lapsed</c> are named exceptions: <c>OfferAsync</c>'s supersede composes
    /// <c>OwnershipOfferPendingButExpired</c>, and <c>OwnershipOfferWrites.LapseNamingAsync</c> is raw SQL whose own
    /// <c>WHERE</c> tests <c>State</c> against <c>Pending</c>. Anchor: the gate finds exactly the three transition methods.
    /// </summary>
    /// <remarks>
    /// <b>Green today, and a standing gate.</b> Removing <c>.Where(OfferPredicates.OwnershipOfferActionable)</c> from
    /// <c>DeclineAsync</c> turns it red. The target state is read as the last <c>ldc.i4</c> before each
    /// <c>SetProperty&lt;OwnershipOfferState&gt;</c> call, so it holds while the states are written as the enum constants.
    /// </remarks>
    [Fact]
    [Trait("Requirement", "AD-26")]
    [Trait("Requirement", "AR-13")]
    [Trait("Requirement", "AD-18")]
    public void Every_OwnershipOffer_state_transition_composes_the_actionable_predicate()
    {
        using var module = Cecil.ModuleDefinition.ReadModule(ProductionAssemblies.Infrastructure.Location);

        var stateType = ProductionAssemblies.Domain.GetType(OwnershipOfferStateTypeName);

        Assert.True(stateType is not null, $"{OwnershipOfferStateTypeName} is not found.");

        int State(string name) => Convert.ToInt32(Enum.Parse(stateType, name), CultureInfo.InvariantCulture);

        var updates = module.GetTypes()
            .Where(type => OutermostName(type) == OwnershipOfferStoreName)
            .SelectMany(type => type.Methods)
            .Where(method => method.HasBody)
            .GroupBy(method => LogicalMethodOf(method).Method, StringComparer.Ordinal)
            .Where(group => group.Any(method => CallsAny(method, call => call is Cecil.GenericInstanceMethod generic
                && generic.Name is "ExecuteUpdate" or "ExecuteUpdateAsync"
                && generic.GenericArguments.Any(argument => argument.FullName == OwnershipOfferTypeName))))
            .ToDictionary(
                group => group.Key,
                group => (
                    States: group.Select(OfferStateSetterValue).OfType<int>().ToHashSet(),
                    Actionable: group.Any(method => CallsAny(method, call => call.Name == "get_OwnershipOfferActionable" && call.DeclaringType.Name == "OfferPredicates")),
                    Expired: group.Any(method => CallsAny(method, call => call.Name == "get_OwnershipOfferPendingButExpired" && call.DeclaringType.Name == "OfferPredicates"))),
                StringComparer.Ordinal);

        string Writing(string state) => string.Join(", ", updates.Where(update => update.Value.States.Contains(State(state))).Select(update => update.Key).Order(StringComparer.Ordinal));

        var found = string.Join("; ", updates.Select(update => $"{update.Key} sets [{string.Join(", ", update.Value.States)}]"));

        Assert.True(Writing("Accepted") == "AcceptAsync", "Expected AcceptAsync alone to write Accepted. Found: " + found);
        Assert.True(Writing("Declined") == "DeclineAsync", "Expected DeclineAsync alone to write Declined. Found: " + found);
        Assert.True(Writing("Revoked") == "RevokeAsync", "Expected RevokeAsync alone to write Revoked. Found: " + found);
        Assert.True(Writing("Lapsed") == "OfferAsync", "Expected OfferAsync alone to write Lapsed. Found: " + found);

        var terminal = new[] { State("Accepted"), State("Declined"), State("Revoked") };
        var unguarded = updates
            .Where(update => update.Value.States.Overlaps(terminal) && !update.Value.Actionable)
            .Select(update => update.Key)
            .ToList();

        Assert.True(unguarded.Count == 0, "These transitions do not compose OwnershipOfferActionable: " + string.Join(", ", unguarded));
        Assert.True(updates["OfferAsync"].Expired, "OfferAsync's supersede does not compose OwnershipOfferPendingButExpired.");

        var writes = module.GetType(OwnershipOfferWritesName);

        Assert.True(writes is not null, $"{OwnershipOfferWritesName} is not found.");

        var lapse = writes.Methods.SingleOrDefault(method => method.Name == "LapseNamingAsync");

        Assert.True(lapse is not null && lapse.HasBody, "OwnershipOfferWrites declares no LapseNamingAsync.");

        var literals = (StateMachineMoveNext(lapse) ?? lapse).Body.Instructions
            .Where(instruction => instruction.OpCode == Cecil.Cil.OpCodes.Ldstr)
            .Select(instruction => instruction.Operand)
            .OfType<string>()
            .ToList();
        var sql = Assert.Single(literals.Distinct(StringComparer.Ordinal), text => OwnershipOfferSqlWrite.IsMatch(text));

        Assert.Matches(LapseGuard, sql);
        Assert.True(literals.Contains("Pending", StringComparer.Ordinal) || sql.Contains("Pending", StringComparison.Ordinal), "The lapse's guard does not compare State with Pending.");
    }

    /// <summary>
    /// A-10, for offers and Invitations alike: no scheduled component reaches a write of an <c>OwnershipOffer</c> or an
    /// <c>Invitation</c> state. The writers are every production method that bulk-writes either entity, calls its
    /// <c>set_State</c>, or holds SQL that writes its table. The scheduled roots are every production type that derives from,
    /// implements or references <c>BackgroundService</c>, <c>IHostedService</c>, <c>IHostedLifecycleService</c>,
    /// <c>PeriodicTimer</c>, <c>System.Threading.Timer</c> or <c>System.Timers.Timer</c>; every method loaded as a delegate
    /// where a timer is made; and every type registered through <c>AddHostedService</c>. The transitive call graph from the
    /// roots - through state machines, lambdas, and from an interface method to every production implementation - reaches no
    /// writer.
    /// </summary>
    /// <remarks>
    /// <b>Green today, and a standing gate.</b> There are no scheduled roots today, so two anchors make the walk real: from
    /// <c>DeclineOwnershipOfferHandler.HandleAsync</c> the same walk reaches <c>OwnershipOfferStore.DeclineAsync</c>, a writer,
    /// through the store's interface; and the writer set holds every transition of both entities. Unlike the timer ban, this
    /// gate still holds A-10 should that ban be lifted for an unrelated reason. Plants: an unregistered
    /// <c>BackgroundService</c> whose <c>ExecuteAsync</c> calls <c>OwnershipOfferWrites.LapseNamingAsync</c>, and a method
    /// passed to <c>new Timer(...)</c> that calls it.
    /// </remarks>
    [Fact]
    [Trait("Requirement", "AR-31")]
    [Trait("Requirement", "AD-27")]
    [Trait("Requirement", "AD-10")]
    public void No_scheduled_component_reaches_an_offer_or_Invitation_state_write()
    {
        string[] scheduled =
        [
            "Microsoft.Extensions.Hosting.BackgroundService",
            "Microsoft.Extensions.Hosting.IHostedService",
            "Microsoft.Extensions.Hosting.IHostedLifecycleService",
            "System.Threading.PeriodicTimer",
            "System.Threading.Timer",
            "System.Timers.Timer",
        ];
        string[] timers = ["System.Threading.PeriodicTimer", "System.Threading.Timer", "System.Timers.Timer"];

        using var graph = new CallGraph();

        var modules = RuntimeAssemblies().Select(assembly => Cecil.ModuleDefinition.ReadModule(assembly.Location)).ToList();

        try
        {
            var types = modules.SelectMany(module => module.GetTypes()).ToList();
            var writers = types
                .SelectMany(type => type.Methods)
                .Where(WritesAnOfferOrInvitationState)
                .Select(LogicalMethodOf)
                .ToHashSet();

            var rootTypes = types
                .Where(type => Array.Exists(scheduled, name => TypeReferences(type, name)))
                .Select(type => type.FullName)
                .Concat(types.SelectMany(type => type.Methods).SelectMany(HostedServicesRegisteredBy))
                .ToHashSet(StringComparer.Ordinal);
            var callbacks = types
                .SelectMany(type => type.Methods)
                .SelectMany(method => DelegatesLoadedWhereATimerIsMade(method, timers))
                .ToList();

            var roots = rootTypes
                .Select(graph.TypeNamed)
                .OfType<Cecil.TypeDefinition>()
                .SelectMany(type => type.Methods)
                .Concat(callbacks.Select(graph.Resolve).OfType<Cecil.MethodDefinition>())
                .ToList();

            // Anchor (ii): the writer set holds every transition of both entities, so a scan that found none fails here.
            Assert.Superset(
                new HashSet<(string, string)>(
                [
                    (OwnershipOfferStoreName, "OfferAsync"),
                    (OwnershipOfferStoreName, "AcceptAsync"),
                    (OwnershipOfferStoreName, "DeclineAsync"),
                    (OwnershipOfferStoreName, "RevokeAsync"),
                    (OwnershipOfferWritesName, "LapseNamingAsync"),
                    (InvitationStoreName, "RevokeAsync"),
                ]),
                writers);
            Assert.Contains(writers, writer => writer.Type == InvitationStoreName && writer.Method.StartsWith("Accept", StringComparison.Ordinal));

            // Anchor (i): the walk crosses a port into a writer, so a walk that reached nothing would fail here.
            var declineHandler = Assert.Single(ProductionAssemblies.Application.GetTypes(), type => type.Name == "DeclineOwnershipOfferHandler");
            var handle = graph.TypeNamed(declineHandler.FullName!)?.Methods.Where(method => method.Name == "HandleAsync").ToList() ?? [];

            Assert.NotEmpty(handle);
            Assert.Contains((OwnershipOfferStoreName, "DeclineAsync"), Reached(graph, handle).Select(LogicalMethodOf).Where(writers.Contains));

            var reached = Reached(graph, roots)
                .Select(LogicalMethodOf)
                .Where(writers.Contains)
                .Select(writer => $"{writer.Type}.{writer.Method}")
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)
                .ToList();

            Assert.True(
                reached.Count == 0,
                $"A scheduled component ({string.Join(", ", rootTypes.Order(StringComparer.Ordinal))}) reaches an offer or Invitation state write; "
                + "expiry is computed on read and nothing scheduled writes a state (AD-27):\n" + string.Join('\n', reached));
        }
        finally
        {
            foreach (var module in modules)
            {
                module.Dispose();
            }
        }
    }

    /// <summary>
    /// Expiry is never the predicate of a write, but for one named exception: <c>OfferPredicates.OwnershipOfferPendingButExpired</c>
    /// is referenced only by <c>OwnershipOfferStore.OfferAsync</c>, where a new offer supersedes an expired one on the request
    /// path, and <c>OfferPredicates</c> declares no other expiry predicate - so no Invitation expiry write exists.
    /// </summary>
    /// <remarks>
    /// <b>Green today, and a standing gate.</b> A second reference to the expired predicate anywhere turns it red.
    /// </remarks>
    [Fact]
    [Trait("Requirement", "AR-31")]
    [Trait("Requirement", "AD-27")]
    public void Only_a_new_offer_composes_the_expired_predicate_and_no_Invitation_expiry_write_exists()
    {
        var modules = RuntimeAssemblies().Select(assembly => Cecil.ModuleDefinition.ReadModule(assembly.Location)).ToList();

        try
        {
            var referencing = modules
                .SelectMany(module => module.GetTypes())
                .SelectMany(type => type.Methods)
                .Where(method => method.HasBody && CallsAny(method, call => call.Name == "get_OwnershipOfferPendingButExpired" && call.DeclaringType.Name == "OfferPredicates"))
                .Select(LogicalMethodOf)
                .Distinct()
                .ToList();

            Assert.Equal([(OwnershipOfferStoreName, "OfferAsync")], referencing);
        }
        finally
        {
            foreach (var module in modules)
            {
                module.Dispose();
            }
        }

        var predicates = Assert.Single(ProductionAssemblies.Infrastructure.GetTypes(), type => type.Name == "OfferPredicates");
        const BindingFlags any = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.DeclaredOnly;
        var expiryMembers = predicates.GetProperties(any).Select(property => property.Name)
            .Concat(predicates.GetFields(any).Where(field => !field.IsDefined(typeof(System.Runtime.CompilerServices.CompilerGeneratedAttribute))).Select(field => field.Name))
            .Concat(predicates.GetMethods(any).Where(method => !method.IsSpecialName).Select(method => method.Name))
            .Where(name => name.Contains("Expired", StringComparison.Ordinal))
            .ToList();

        Assert.Equal(["OwnershipOfferPendingButExpired"], expiryMembers);
    }

    /// <summary>
    /// The method <paramref name="method"/> was written as: an async method's state machine, a lambda and a local function are
    /// attributed to the method that declares them, and the type to the outermost type that is not compiler-generated.
    /// </summary>
    private static (string Type, string Method) LogicalMethodOf(Cecil.MethodDefinition method)
    {
        var logical = method.Name.StartsWith('<') ? CompilerGeneratedOwner(method.Name) : null;
        var type = method.DeclaringType;

        while (type.DeclaringType is not null && type.Name.StartsWith('<'))
        {
            if (logical is null && !type.Name.StartsWith("<>", StringComparison.Ordinal))
            {
                logical = CompilerGeneratedOwner(type.Name);
            }

            type = type.DeclaringType;
        }

        return (type.FullName, logical ?? method.Name);
    }

    /// <summary>
    /// The source method a compiler-generated name such as <c>&lt;DeclineAsync&gt;d__12</c> or <c>&lt;DeclineAsync&gt;b__12_0</c>
    /// belongs to, or <c>null</c> for one that names none.
    /// </summary>
    private static string? CompilerGeneratedOwner(string name)
    {
        var end = name.IndexOf('>', StringComparison.Ordinal);

        return end > 1 ? name[1..end] : null;
    }

    /// <summary>
    /// Whether <paramref name="method"/> writes an <c>OwnershipOffer</c> or an <c>Invitation</c> state: a bulk write generic
    /// over either entity, a call to its <c>set_State</c>, or SQL text that writes its table.
    /// </summary>
    private static bool WritesAnOfferOrInvitationState(Cecil.MethodDefinition method) =>
        method.HasBody && method.Body.Instructions.Any(instruction => instruction.Operand switch
        {
            Cecil.GenericInstanceMethod generic when OfferBulkWrites.Contains(generic.Name, StringComparer.Ordinal) =>
                generic.GenericArguments.Any(argument => argument.FullName is OwnershipOfferTypeName or InvitationTypeName),
            Cecil.MethodReference called when called.Name == "set_State" => called.DeclaringType.FullName is OwnershipOfferTypeName or InvitationTypeName,
            string text when instruction.OpCode == Cecil.Cil.OpCodes.Ldstr => OwnershipOfferSqlWrite.IsMatch(text) || InvitationSqlWrite.IsMatch(text),
            _ => false,
        });

    /// <summary>
    /// The types <paramref name="method"/> registers through <c>AddHostedService</c>.
    /// </summary>
    private static IEnumerable<string> HostedServicesRegisteredBy(Cecil.MethodDefinition method) =>
        method.HasBody
            ? method.Body.Instructions
                .Select(instruction => instruction.Operand)
                .OfType<Cecil.GenericInstanceMethod>()
                .Where(called => called.Name == "AddHostedService")
                .SelectMany(called => called.GenericArguments.Select(argument => argument.GetElementType().FullName))
            : [];

    /// <summary>
    /// Every method <paramref name="method"/> loads as a delegate, when it also makes or touches a timer: a timer's callback.
    /// </summary>
    private static IEnumerable<Cecil.MethodReference> DelegatesLoadedWhereATimerIsMade(Cecil.MethodDefinition method, string[] timers)
    {
        if (!method.HasBody || !CallsAny(method, called => timers.Contains(called.DeclaringType.FullName, StringComparer.Ordinal)))
        {
            return [];
        }

        return method.Body.Instructions
            .Where(instruction => instruction.OpCode == Cecil.Cil.OpCodes.Ldftn || instruction.OpCode == Cecil.Cil.OpCodes.Ldvirtftn)
            .Select(instruction => instruction.Operand)
            .OfType<Cecil.MethodReference>();
    }

    /// <summary>
    /// Whether any call, delegate load or other method reference in <paramref name="method"/>'s body satisfies
    /// <paramref name="matches"/>.
    /// </summary>
    private static bool CallsAny(Cecil.MethodDefinition method, Func<Cecil.MethodReference, bool> matches) =>
        method.HasBody && method.Body.Instructions.Any(instruction => instruction.Operand is Cecil.MethodReference called && matches(called));

    /// <summary>
    /// When <paramref name="method"/> calls <c>SetProperty&lt;OwnershipOfferState&gt;</c>, the state it sets: the last
    /// <c>ldc.i4</c> before the call. <c>null</c> otherwise.
    /// </summary>
    private static int? OfferStateSetterValue(Cecil.MethodDefinition method)
    {
        int? last = null;

        foreach (var instruction in method.Body.Instructions)
        {
            if (LoadedConstant(instruction) is { } loaded)
            {
                last = loaded;
            }

            if (instruction.Operand is Cecil.GenericInstanceMethod { Name: "SetProperty" } generic
                && generic.GenericArguments.Any(argument => argument.FullName == OwnershipOfferStateTypeName))
            {
                return last;
            }
        }

        return null;
    }

    /// <summary>
    /// The integer an <c>ldc.i4</c> instruction in any of its short forms loads, or <c>null</c> for any other instruction.
    /// </summary>
    private static int? LoadedConstant(Cecil.Cil.Instruction instruction) => instruction.OpCode.Code switch
    {
        Cecil.Cil.Code.Ldc_I4_M1 => -1,
        Cecil.Cil.Code.Ldc_I4_0 => 0,
        Cecil.Cil.Code.Ldc_I4_1 => 1,
        Cecil.Cil.Code.Ldc_I4_2 => 2,
        Cecil.Cil.Code.Ldc_I4_3 => 3,
        Cecil.Cil.Code.Ldc_I4_4 => 4,
        Cecil.Cil.Code.Ldc_I4_5 => 5,
        Cecil.Cil.Code.Ldc_I4_6 => 6,
        Cecil.Cil.Code.Ldc_I4_7 => 7,
        Cecil.Cil.Code.Ldc_I4_8 => 8,
        Cecil.Cil.Code.Ldc_I4_S when instruction.Operand is sbyte small => small,
        Cecil.Cil.Code.Ldc_I4 when instruction.Operand is int value => value,
        _ => null,
    };

    /// <summary>
    /// Every production method reachable from <paramref name="roots"/>, by the walk <c>HandlerReaches</c> uses: into the
    /// state machine and local functions a method is compiled into, from an interface or abstract method into every production
    /// implementation, and through every call and delegate load into a production type.
    /// </summary>
    private static HashSet<Cecil.MethodDefinition> Reached(CallGraph graph, IEnumerable<Cecil.MethodDefinition> roots)
    {
        var queue = new Queue<Cecil.MethodDefinition>();
        var seen = new HashSet<Cecil.MethodDefinition>();

        void Enqueue(Cecil.MethodDefinition method)
        {
            if (seen.Add(method))
            {
                queue.Enqueue(method);
            }
        }

        foreach (var root in roots)
        {
            Enqueue(root);
        }

        while (queue.TryDequeue(out var method))
        {
            foreach (var nested in method.DeclaringType.NestedTypes
                .Where(nested => nested.Name.StartsWith($"<{method.Name}>", StringComparison.Ordinal))
                .SelectMany(nested => nested.Methods))
            {
                Enqueue(nested);
            }

            foreach (var implementation in graph.ImplementationsOf(method))
            {
                Enqueue(implementation);
            }

            if (!method.HasBody)
            {
                continue;
            }

            foreach (var called in method.Body.Instructions
                .Select(instruction => instruction.Operand)
                .OfType<Cecil.MethodReference>()
                .Select(graph.Resolve)
                .OfType<Cecil.MethodDefinition>())
            {
                Enqueue(called);
            }
        }

        return seen;
    }

    [GeneratedRegex(@"(?i)\b(UPDATE|INSERT\s+INTO|DELETE\s+FROM|DELETE|MERGE(\s+INTO)?)\s+(dbo\.)?\[?Invitation\]?(\s|$|\W)", RegexOptions.None, 1000)]
    private static partial Regex InvitationSqlWrite { get; }

    [GeneratedRegex(@"(?is)\bWHERE\b.*\bState\s*=\s*(\{\d+\}|N?'?Pending'?)", RegexOptions.None, 1000)]
    private static partial Regex LapseGuard { get; }
}
