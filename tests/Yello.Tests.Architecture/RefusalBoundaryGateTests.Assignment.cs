using System.Reflection;
using System.Xml.Linq;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Xunit;
using Yello.Contracts;
using Yello.Domain;
using Yello.Domain.Memberships;
using Yello.Domain.Tasks;
using Yello.Host.Spaces;
using Yello.Infrastructure.Persistence;

namespace Yello.Tests.Architecture;

/// <summary>
/// Structural gates for assigning a Task: the two new routes and the one capability each declares, the matrix's own
/// <c>AssignTask</c> row reaching exactly the assignment route, the assignment contracts (no address, no Role, no Space), the
/// Task answer's appended Assignee, the composite Assignee key in the EF model, the email template's inputs, the send
/// record's absence of readers, the problem type and its client copy, and the client's assign control agreeing with the
/// matrix.
/// </summary>
/// <remarks>
/// <para>
/// <b>Each gate finds its subject by name or by shape</b> (route pattern, reflection, Cecil, the EF model, the resource
/// file), so a subject built under another name turns it red. The names are the story's Project Structure Notes:
/// <c>Yello.Contracts.Tasks.AssignTaskRequest</c>, <c>Yello.Contracts.Memberships.AssigneeResponse</c> and
/// <c>AssigneeListResponse</c>, <c>TaskItem.AssigneeMembershipId</c>, <c>AssignmentEmail</c>, <c>NotificationSend</c>,
/// <c>OutboxKinds.TaskAssigned</c>, and a client predicate named for <c>AssignTask</c>. A different name is corrected here
/// first and recorded in the Debug Log.
/// </para>
/// <para>
/// <b>Existing gates this work must keep green, unchanged:</b>
/// <c>Only_the_Membership_list_and_the_Invitation_list_carry_an_email_address</c>,
/// <c>Only_named_types_read_an_Accounts_email_address</c>, <c>Attribution_never_reads_a_live_Account_name</c>,
/// <c>Only_the_outbox_store_its_dispatcher_and_its_configuration_refer_to_the_outbox_row</c>,
/// <c>The_leave_route_is_the_one_write_under_ReadProjectsAndTasks</c> (the candidates read is a <c>GET</c>),
/// <c>The_Task_routes_declare_their_capability_serve_both_surfaces_and_consult_no_bound</c> (the PATCH is unchanged), both
/// Role-read gates, <c>Every_reference_between_Space_scoped_entities_carries_SpaceId</c>, and
/// <c>No_production_code_runs_on_a_timer_or_as_a_hosted_service</c>. Two existing gates are widened by exactly the Assignee
/// when it lands: <c>The_due_date_is_a_nullable_DateTimeOffset_on_the_Task_and_its_response</c> and
/// <c>The_Task_response_gains_its_Label_ids_and_keeps_every_earlier_field</c>, which pin <c>TaskResponse</c>'s members.
/// </para>
/// </remarks>
public sealed partial class RefusalBoundaryGateTests
{
    private const string AssigneePattern = "/api/v1/spaces/{spaceId:guid}/tasks/{taskId:guid}/assignee";

    private const string AssigneesPattern = "/api/v1/spaces/{spaceId:guid}/assignees";

    /// <summary>
    /// The two assignment routes are exactly these, each with its one capability, neither Session-only, neither consulting a
    /// bound: <c>PUT .../tasks/{taskId}/assignee</c> under <c>AssignTask</c> and <c>GET .../assignees</c>
    /// under <c>ReadProjectsAndTasks</c> (every Role reads the candidates). A Token reaches both (AD-4).
    /// </summary>
    [Fact(Skip = "red: AC1, AC13 - neither assignment route is mapped yet (Lee's Q1 (a), Q2 (a))")]
    [Trait("Requirement", "FR-21")]
    [Trait("Requirement", "AD-4")]
    [Trait("Requirement", "AD-1")]
    public void The_assignment_routes_declare_one_capability_each_and_serve_both_surfaces()
    {
        var expected = new Dictionary<string, Capability>(StringComparer.Ordinal)
        {
            [$"PUT {AssigneePattern}"] = Capability.AssignTask,
            [$"GET {AssigneesPattern}"] = Capability.ReadProjectsAndTasks,
        };
        var routes = RouteTable()
            .Where(endpoint => endpoint.RoutePattern.RawText is AssigneePattern or AssigneesPattern)
            .SelectMany(endpoint => (endpoint.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods ?? [])
                .Select(method => (Key: $"{method} {endpoint.RoutePattern.RawText}", Endpoint: endpoint)))
            .ToDictionary(route => route.Key, route => route.Endpoint, StringComparer.Ordinal);

        Assert.Equal(expected.Keys.Order(StringComparer.Ordinal), routes.Keys.Order(StringComparer.Ordinal));

        foreach (var (key, endpoint) in routes)
        {
            Assert.True(
                endpoint.Metadata.GetMetadata<RequiredCapability>()?.Capability == expected[key],
                $"{key} declares {endpoint.Metadata.GetMetadata<RequiredCapability>()?.Capability}; expected {expected[key]}.");
            Assert.DoesNotContain(endpoint.Metadata, metadata => metadata.GetType().Name == "SessionOnlyEndpoint");
            Assert.Null(endpoint.Metadata.GetMetadata<RequiredBound>());
        }
    }

    /// <summary>
    /// The matrix's "Assign a Task" row governs exactly one route: across every mapped endpoint, <c>AssignTask</c> is required
    /// by the assignment route alone. It was transcribed and used by nothing until now; this keeps a second route from
    /// borrowing it and the assignment from being folded into the PATCH under another capability.
    /// </summary>
    [Fact(Skip = "red: AC13 - no endpoint requires Capability.AssignTask yet")]
    [Trait("Requirement", "FR-16")]
    [Trait("Requirement", "AD-1")]
    public void AssignTask_is_required_by_exactly_the_assignment_route()
    {
        var requiring = RouteTable()
            .Where(endpoint => endpoint.Metadata.GetMetadata<RequiredCapability>()?.Capability == Capability.AssignTask)
            .SelectMany(endpoint => (endpoint.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods ?? [])
                .Select(method => $"{method} {endpoint.RoutePattern.RawText}"))
            .ToList();

        Assert.Equal([$"PUT {AssigneePattern}"], requiring);
    }

    /// <summary>
    /// AD-23, UX-DR34: the assignment contracts carry no address, Role, Account id or Space id. <c>AssignTaskRequest</c> is
    /// exactly <c>MembershipId</c>, a <c>Guid?</c> (<c>null</c> unassigns); <c>AssigneeResponse</c> is exactly
    /// <c>MembershipId</c> (a <c>Guid</c>) and <c>DisplayName</c> (a string); <c>AssigneeListResponse</c> is exactly
    /// <c>Assignees</c>, unpaged.
    /// </summary>
    [Fact(Skip = "red: AC1, AC6 - AssignTaskRequest, AssigneeResponse and AssigneeListResponse are not declared yet")]
    [Trait("Requirement", "AD-23")]
    [Trait("Requirement", "UX-DR34")]
    public void The_assignment_contracts_carry_exactly_their_named_members()
    {
        var request = ProductionAssemblies.Contracts.GetType("Yello.Contracts.Tasks.AssignTaskRequest");
        var candidate = ProductionAssemblies.Contracts.GetType("Yello.Contracts.Memberships.AssigneeResponse");
        var list = ProductionAssemblies.Contracts.GetType("Yello.Contracts.Memberships.AssigneeListResponse");

        Assert.True(request is not null && candidate is not null && list is not null, "AssignTaskRequest, AssigneeResponse or AssigneeListResponse is not declared.");
        Assert.Equal(["MembershipId"], PropertyNames(request));
        Assert.Equal(typeof(Guid?), request.GetProperty("MembershipId")?.PropertyType);
        Assert.Equal(["DisplayName", "MembershipId"], PropertyNames(candidate));
        Assert.Equal(typeof(Guid), candidate.GetProperty("MembershipId")?.PropertyType);
        Assert.Equal(typeof(string), candidate.GetProperty("DisplayName")?.PropertyType);
        Assert.Equal(["Assignees"], PropertyNames(list));
    }

    /// <summary>
    /// AD-19: <c>TaskResponse</c> gains <c>AssigneeMembershipId</c> as a <c>Guid?</c> and keeps every earlier field - a field
    /// added, none removed or retyped - and still carries no name, address or <c>SpaceId</c>. The successor of 2.4's
    /// response-shape gate.
    /// </summary>
    [Fact(Skip = "red: AC10 - TaskResponse has no AssigneeMembershipId yet")]
    [Trait("Requirement", "AD-19")]
    [Trait("Requirement", "FR-21")]
    public void The_Task_response_gains_its_Assignee_and_keeps_every_earlier_field()
    {
        var response = typeof(Yello.Contracts.Tasks.TaskResponse);

        Assert.Equal(["AssigneeMembershipId", "CreatedAt", "DueDate", "Id", "LabelIds", "PositionKey", "StatusId", "Title"], PropertyNames(response));
        Assert.Equal(typeof(Guid?), response.GetProperty("AssigneeMembershipId")?.PropertyType);
        Assert.Equal(typeof(IReadOnlyList<Guid>), response.GetProperty("LabelIds")?.PropertyType);
    }

    /// <summary>
    /// AR-8, AD-2, AD-5 in the EF model: <c>TaskItem.AssigneeMembershipId</c> is a <c>Guid?</c>, and its one reference to
    /// <c>Membership</c> is <c>(SpaceId, AssigneeMembershipId) → (SpaceId, Id)</c> with <c>DeleteBehavior.NoAction</c> - not
    /// SetNull (it would null <c>SpaceId</c>), not Cascade (it would delete the Task).
    /// </summary>
    [Fact(Skip = "red: AC2 - TaskItem has no AssigneeMembershipId and the model no Assignee key yet")]
    [Trait("Requirement", "AR-8")]
    [Trait("Requirement", "AD-2")]
    [Trait("Requirement", "AD-5")]
    public void The_Assignee_is_a_composite_no_action_reference_to_Membership_in_the_model()
    {
        Assert.Equal(typeof(Guid?), typeof(TaskItem).GetProperty("AssigneeMembershipId")?.PropertyType);

        using var context = new YelloDbContextFactory().CreateDbContext([]);

        var task = context.Model.FindEntityType(typeof(TaskItem));

        Assert.NotNull(task);

        var toMembership = Assert.Single(task.GetForeignKeys(), key => key.PrincipalEntityType.ClrType == typeof(Membership));

        Assert.Equal(["SpaceId", "AssigneeMembershipId"], toMembership.Properties.Select(property => property.Name));
        Assert.Equal(["SpaceId", "Id"], toMembership.PrincipalKey.Properties.Select(property => property.Name));
        Assert.Equal(DeleteBehavior.NoAction, toMembership.DeleteBehavior);
    }

    /// <summary>
    /// FR-40, I-14: the assignment email template's inputs are its content contract. Every public constructor and public
    /// static method of the type named <c>AssignmentEmail</c> takes only strings and a URI - the assigner's name, the Space's,
    /// the Project's, the Task's title and the client's base URL - so no Domain type can be passed in and nothing of another
    /// Space can reach the email. Sibling of the Invitation template's gate.
    /// </summary>
    [Fact(Skip = "red: AC4, AC13 - AssignmentEmail is not declared yet (Q8 (a))")]
    [Trait("Requirement", "FR-40")]
    [Trait("Requirement", "NFR-1")]
    public void The_assignment_email_template_takes_strings_and_a_URI_and_nothing_else()
    {
        var template = Assert.Single(
            ProductionAssemblies.All.SelectMany(assembly => assembly.GetTypes()),
            type => type.Name == "AssignmentEmail");

        var entries = template.GetConstructors(BindingFlags.Public | BindingFlags.Instance)
            .Cast<MethodBase>()
            .Concat(template.GetMethods(BindingFlags.Public | BindingFlags.Static).Where(method => !method.IsSpecialName && method.DeclaringType == template))
            .ToList();

        Type[] admitted = [typeof(string), typeof(Uri)];

        Assert.NotEmpty(entries);
        Assert.All(entries, entry => Assert.All(entry.GetParameters(), parameter =>
            Assert.True(admitted.Contains(parameter.ParameterType), $"{template.Name}.{entry.Name} takes {parameter.ParameterType.Name} {parameter.Name}.")));
        Assert.Contains(entries, entry => entry.GetParameters().Length == 5);
        Assert.DoesNotContain(entries, entry => entry.GetParameters().Length > 5);
    }

    /// <summary>
    /// FS-NFR-2 ("no product surface reads it"): only the outbox store that writes it, the entity, its configuration and the
    /// context refer to the send record type, so no slice, endpoint or client code can read it. Anchor: the store is among the
    /// dependents. Sibling of the outbox row's reader gate.
    /// </summary>
    [Fact(Skip = "red: AC5 - NotificationSend is not declared yet (Lee's Q4 (a))")]
    [Trait("Requirement", "FS-NFR-2")]
    [Trait("Requirement", "AD-2")]
    public void Only_the_outbox_store_and_its_configuration_refer_to_the_send_record()
    {
        var record = Assert.Single(ProductionAssemblies.All.SelectMany(assembly => assembly.GetTypes()), type => type.Name == "NotificationSend");
        var dependents = DependentsOf(record.FullName!);

        string[] named = ["OutboxStore", "NotificationSendConfiguration", "YelloDbContext", "NotificationSend"];

        Assert.Contains(dependents, dependent => dependent.EndsWith(".OutboxStore", StringComparison.Ordinal));

        var others = dependents.Where(dependent => !named.Contains(dependent.Split('.')[^1], StringComparer.Ordinal)).ToList();

        Assert.True(others.Count == 0, "The send record is reached outside the named writers:\n" + string.Join('\n', others));
    }

    /// <summary>
    /// The assignment email's kind is a stable code beside the Invitation's: <c>OutboxKinds.TaskAssigned</c> is
    /// <c>task-assigned</c>, the value the send record stores.
    /// </summary>
    [Fact(Skip = "red: AC4 - OutboxKinds.TaskAssigned is not declared yet")]
    [Trait("Requirement", "FR-40")]
    [Trait("Requirement", "FS-NFR-2")]
    public void The_assignment_email_has_its_own_outbox_kind()
    {
        var kinds = Assert.Single(ProductionAssemblies.All.SelectMany(assembly => assembly.GetTypes()), type => type.Name == "OutboxKinds");

        Assert.Equal("task-assigned", kinds.GetProperty("TaskAssigned", BindingFlags.Public | BindingFlags.Static)?.GetValue(null));
        Assert.Equal("invitation-issued", kinds.GetProperty("InvitationIssued", BindingFlags.Public | BindingFlags.Static)?.GetValue(null));
    }

    /// <summary>
    /// <c>invalid-assignee</c> is declared in <c>ProblemTypes</c> and has client copy of its own in <c>ClientCopy.resx</c>, with
    /// a translator's comment.
    /// </summary>
    [Fact(Skip = "red: AC1, AC10 - ProblemTypes.InvalidAssignee and its client copy are not declared yet")]
    [Trait("Requirement", "AR-34")]
    [Trait("Requirement", "FR-21")]
    public void The_invalid_assignee_problem_type_is_declared_and_has_client_copy()
    {
        var declared = typeof(ProblemTypes)
            .GetProperties(BindingFlags.Public | BindingFlags.Static)
            .Select(property => property.GetValue(null) as string)
            .ToHashSet(StringComparer.Ordinal);

        var copy = XDocument.Load(Path.Combine(RepositoryLayout.Root.FullName, "Yello.Client", "Localisation", "ClientCopy.resx"))
            .Root!
            .Elements("data")
            .ToDictionary(data => (string)data.Attribute("name")!, data => (string?)data.Element("comment") ?? string.Empty, StringComparer.Ordinal);

        Assert.Contains("urn:yello:problem:invalid-assignee", declared);
        Assert.True(copy.TryGetValue("invalid-assignee", out var comment), "ClientCopy.resx has no entry for invalid-assignee.");
        Assert.False(string.IsNullOrWhiteSpace(comment), "The invalid-assignee copy has no translator's comment.");
    }

    /// <summary>
    /// UX-DR27: the client's predicate for showing the assign control agrees with the capability matrix for every Role: shown
    /// exactly where <c>AssignTask</c> is not denied (Owner, Admin, Member), absent for a Viewer. Sibling of
    /// <c>The_clients_Task_controls_predicate_agrees_with_the_capability_matrix</c>.
    /// </summary>
    [Fact(Skip = "red: AC6 - the client has no AssignTask predicate yet (TaskCapabilities.PermitsAssignTask)")]
    [Trait("Requirement", "UX-DR27")]
    [Trait("Requirement", "AD-1")]
    public void The_clients_assign_control_predicate_agrees_with_the_capability_matrix()
    {
        var predicate = ClientRolePredicate("AssignTask");

        foreach (var role in Enum.GetValues<Role>())
        {
            var expected = RoleCapabilityMatrix.Decide(role, Capability.AssignTask) != CapabilityDecision.Denied;

            Assert.True(
                predicate.Invoke(null, [StableCode.Of(role)]) is bool shown && shown == expected,
                $"The client's assign predicate disagrees with the matrix for {role}.");
        }
    }
}
