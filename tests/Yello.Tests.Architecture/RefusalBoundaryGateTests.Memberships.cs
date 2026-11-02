using System.Collections;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.AspNetCore.Routing;
using Mono.Cecil;
using Xunit;
using Yello.Contracts;
using Yello.Domain;
using Yello.Domain.Accounts;
using Yello.Domain.Memberships;
using Yello.Host.Spaces;

namespace Yello.Tests.Architecture;

/// <summary>
/// Story 4.4's structural gates for listing Memberships and changing a Role: the routes and their one capability on both
/// surfaces (AC1, AC4; Lee's Q4 (a)), the list row's contract (AC1), where an email address may appear and who may read one
/// (AC2; Lee's Q4 (a)), who may read an Account's display name (AC7; Lee's Q5 (a)), the pipeline-owned acting authority and
/// the Qualified cells' target rules (AC4; Lee's Q1 (a); ledger <c>:329</c>), the new problem types and their copy (AC4,
/// AC5; Q10 (a)), and the client's Role offer and its absent-never-disabled controls (AC4; UX-DR11, UX-DR27). Written by the
/// story 4.4 ATDD run, 2026-11-02.
/// </summary>
/// <remarks>
/// <para>
/// <b>Red-phase scaffolds, activated by the dev phase.</b> Each gate finds its subject by name (route pattern, reflection,
/// Cecil or the resource file), so a renamed subject turns it red. The names are the story's Project Structure Notes:
/// <c>Yello.Contracts.Memberships.MembershipResponse</c>, <c>Yello.Infrastructure.Persistence.MembershipStore</c>, a holder
/// in <c>Yello.Application</c> whose name contains <c>Authority</c>, <c>MembershipCapabilities</c> in the client. A
/// different name is corrected here first and recorded in the Debug Log.
/// </para>
/// <para>
/// <b>Green today, left active</b> (standing regression gates, each with a detector oracle run against a plant in this
/// assembly): <see cref="Only_the_Membership_list_and_the_Invitation_list_carry_an_email_address"/>,
/// <see cref="Only_named_types_read_an_Accounts_email_address"/> and
/// <see cref="Attribution_never_reads_a_live_Account_name"/>. Their permitted lists already name 4.4's
/// <c>MembershipStore</c> and <c>MembershipResponse</c>, which do not exist yet; a name that never appears admits nothing.
/// The two Role-read gates the story must leave unwidened
/// (<c>Nothing_but_the_pipeline_reads_the_Role_off_the_active_Space_context</c>,
/// <c>No_slice_or_endpoint_reads_the_Role_off_a_Membership_row</c>) and the no-cache gate
/// (<c>SpaceResolutionSeamTests.No_production_code_introduces_a_cache</c>) are existing gates, not duplicated here.
/// </para>
/// </remarks>
public sealed partial class RefusalBoundaryGateTests
{
    private const string MembershipsPattern = "/api/v1/spaces/{spaceId:guid}/memberships";

    private const string MembershipPattern = "/api/v1/spaces/{spaceId:guid}/memberships/{membershipId:guid}";

    private const string AccountTypeName = "Yello.Domain.Accounts.Account";

    private const string MembershipRoutesPending =
        "red: AC1, AC4 - story 4.4 maps GET .../memberships and PATCH .../memberships/{membershipId} under ChangeMembershipRole (Lee's Q4 (a); Q9 (a))";

    private const string AuthorityPending =
        "red: AC4 - Lee's Q1 (a): the Qualified cells' target rules in RoleCapabilityMatrix and a pipeline-owned acting-authority holder (Tasks 2, 4, 6)";

    private const string MembershipContractPending =
        "red: AC1, AC4, AC5 - story 4.4 declares MembershipResponse, owner-role-fixed and role-not-assignable (Task 3; Q10 (a))";

    private const string MembershipClientPending =
        "red: AC4, AC10 - story 4.4 builds MembershipSection, MembershipRow and MembershipCapabilities in the client (Task 7; Q12 (a))";

    /// <summary>
    /// Types whose public string properties may carry an address (AC2, Lee's Q4 (a)): the two lists that show one, and the
    /// three requests that send one in.
    /// </summary>
    private static readonly string[] StringMembers = ["DisplayName", "EmailAddress", "Role"];

    private static readonly string[] AddressCarryingContracts =
    [
        "Yello.Contracts.Invitations.InvitationResponse",
        "Yello.Contracts.Memberships.MembershipResponse",
        "Yello.Contracts.Invitations.IssueInvitationRequest",
        "Yello.Contracts.Accounts.RegisterAccountRequest",
        "Yello.Contracts.Accounts.SignInRequest",
    ];

    /// <summary>
    /// The production types that may read an Account's address (AC2, Lee's Q4 (a)): the entity and its configuration, the
    /// registration and credential (sign-in) stores and slices, the Invitation store (the issue's already-a-member check and
    /// the accept's caller address), and 4.4's Membership store's list projection.
    /// </summary>
    private static readonly string[] AddressReaders =
    [
        AccountTypeName,
        "Yello.Infrastructure.Persistence.Configurations.AccountConfiguration",
        "Yello.Infrastructure.Persistence.AccountRegistrationStore",
        "Yello.Infrastructure.Persistence.AccountCredentialStore",
        "Yello.Infrastructure.Persistence.InvitationStore",
        "Yello.Infrastructure.Persistence.MembershipStore",
    ];

    /// <summary>
    /// The production types that may read an Account's display name (AC7, Lee's Q5 (a)): the entity and its configuration,
    /// the Account-scoped registration slice and store, the Invitation store (the issue captures <c>IssuerName</c>), and 4.4's
    /// Membership store's list projection, for current Members only.
    /// </summary>
    private static readonly string[] DisplayNameReaders =
    [
        AccountTypeName,
        "Yello.Infrastructure.Persistence.Configurations.AccountConfiguration",
        "Yello.Infrastructure.Persistence.AccountRegistrationStore",
        "Yello.Infrastructure.Persistence.InvitationStore",
        "Yello.Infrastructure.Persistence.MembershipStore",
    ];

    /// <summary>
    /// AC1, AC4, AD-4, Lee's Q4 (a) [SECURITY] - the Membership routes are exactly <c>GET</c> on the collection and
    /// <c>PATCH</c> on the item; both require <c>ChangeMembershipRole</c> (the list carries addresses, section 6.1 and AD-23;
    /// never <c>ReadProjectsAndTasks</c>, and no new matrix row), neither is Session-only (a Token reaches both, AD-4 parity)
    /// and neither consults a bound. Sibling of <c>The_Invitation_routes_all_require_IssueAndRevokeInvitations_and_serve_both_surfaces</c>.
    /// </summary>
    [Fact(Skip = MembershipRoutesPending)]
    [Trait("Requirement", "FR-13")]
    [Trait("Requirement", "AD-4")]
    [Trait("Requirement", "AD-23")]
    public void The_Membership_routes_require_ChangeMembershipRole_and_serve_both_surfaces()
    {
        var routes = RouteTable()
            .Where(endpoint => endpoint.RoutePattern.RawText is MembershipsPattern or MembershipPattern)
            .SelectMany(endpoint => (endpoint.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods ?? [])
                .Select(method => (Key: $"{method} {endpoint.RoutePattern.RawText}", Endpoint: endpoint)))
            .ToDictionary(route => route.Key, route => route.Endpoint, StringComparer.Ordinal);

        Assert.Equal([$"GET {MembershipsPattern}", $"PATCH {MembershipPattern}"], routes.Keys.Order(StringComparer.Ordinal));
        Assert.All(routes.Values, endpoint => Assert.Equal(Capability.ChangeMembershipRole, endpoint.Metadata.GetMetadata<RequiredCapability>()?.Capability));
        Assert.All(routes.Values, endpoint => Assert.DoesNotContain(endpoint.Metadata, metadata => metadata.GetType().Name == "SessionOnlyEndpoint"));
        Assert.All(routes.Values, endpoint => Assert.Null(endpoint.Metadata.GetMetadata<RequiredBound>()));
        Assert.Equal(Enum.GetValues<Capability>().Length, Enum.GetNames<Capability>().Distinct(StringComparer.Ordinal).Count());
    }

    /// <summary>
    /// AC1, AD-23 - the list's contract: <c>MembershipResponse</c> carries exactly <c>Id</c> (a Guid), <c>DisplayName</c>,
    /// <c>EmailAddress</c> and <c>Role</c> (strings) and <c>IsYou</c> (a bool) - never the Account id, the normalised address,
    /// the Space or the join date; <c>MembershipPageResponse</c> carries exactly <c>Total</c> (an int), <c>Memberships</c> and
    /// <c>NextAfter</c> (a nullable string); <c>ChangeMembershipRoleRequest</c> carries exactly <c>Role</c>. Plant 11's
    /// sibling: a sixth property.
    /// </summary>
    [Fact(Skip = MembershipContractPending)]
    [Trait("Requirement", "UX-DR14")]
    [Trait("Requirement", "AD-23")]
    public void The_Membership_contracts_carry_exactly_their_named_members()
    {
        var response = ProductionAssemblies.Contracts.GetType("Yello.Contracts.Memberships.MembershipResponse");
        var page = ProductionAssemblies.Contracts.GetType("Yello.Contracts.Memberships.MembershipPageResponse");
        var request = ProductionAssemblies.Contracts.GetType("Yello.Contracts.Memberships.ChangeMembershipRoleRequest");

        Assert.True(response is not null && page is not null && request is not null, "MembershipResponse, MembershipPageResponse or ChangeMembershipRoleRequest is not declared in Yello.Contracts.Memberships.");
        Assert.Equal(["DisplayName", "EmailAddress", "Id", "IsYou", "Role"], PropertyNames(response));
        Assert.Equal(typeof(Guid), response.GetProperty("Id")?.PropertyType);
        Assert.Equal(typeof(bool), response.GetProperty("IsYou")?.PropertyType);
        Assert.All(StringMembers, name => Assert.Equal(typeof(string), response.GetProperty(name)?.PropertyType));
        Assert.Equal(["Memberships", "NextAfter", "Total"], PropertyNames(page));
        Assert.Equal(typeof(int), page.GetProperty("Total")?.PropertyType);
        Assert.Equal(["Role"], PropertyNames(request));
    }

    /// <summary>
    /// AC2, section 6.1, AD-23, Lee's Q4 (a) - email addresses appear in two response contracts and nowhere else: scanning
    /// every public type in <c>Yello.Contracts</c> for a public string property whose name contains <c>Email</c> or
    /// <c>Address</c>, only the Invitation list's and the Membership list's responses, and the three requests that send an
    /// address in (registration, sign-in, issue), are found. <c>AddressedToYou</c> (a <c>bool?</c>) is no address. Green today,
    /// left active; the Assignee picker (4.6) and Presence (7.5) inherit it. Anchor: <c>InvitationResponse</c> is found.
    /// Detector: <see cref="The_address_contract_scan_detects_a_plant"/>. Plant 11.
    /// </summary>
    [Fact]
    [Trait("Requirement", "UX-DR34")]
    [Trait("Requirement", "AD-23")]
    public void Only_the_Membership_list_and_the_Invitation_list_carry_an_email_address()
    {
        var carriers = AddressCarriers(ProductionAssemblies.Contracts.GetExportedTypes());

        Assert.Contains("Yello.Contracts.Invitations.InvitationResponse", carriers);

        var offenders = carriers.Where(carrier => !AddressCarryingContracts.Contains(carrier, StringComparer.Ordinal)).ToList();

        Assert.True(offenders.Count == 0, "A contract other than the Membership and Invitation lists carries an address (section 6.1, UX-DR34): " + string.Join(", ", offenders));
    }

    /// <summary>
    /// The detector oracle for the contract scan: a planted response type with an <c>EmailAddress</c> is found, and a type
    /// with only a <c>bool AddressedToYou</c> is not.
    /// </summary>
    [Fact]
    [Trait("Requirement", "AD-23")]
    public void The_address_contract_scan_detects_a_plant()
    {
        var carriers = AddressCarriers([typeof(PlantedPresenceResponse), typeof(PlantedOfferResponse)]);

        Assert.Equal([typeof(PlantedPresenceResponse).FullName!], carriers);
    }

    /// <summary>
    /// AC2, AD-23, Lee's Q4 (a) - only named types read an Account's address (<c>Account.EmailAddress</c> or
    /// <c>NormalizedEmailAddress</c>, a getter call or an expression-tree token in the IL): the entity and its configuration,
    /// the registration and credential stores, the Invitation store, and 4.4's Membership store. A slice, an endpoint or a
    /// later story's projection that reaches for the address fails here. Green today, left active. Anchor: the Invitation
    /// store is found reading it. Detector: <see cref="The_Account_reader_scan_detects_a_plant"/>. Plant 12.
    /// </summary>
    [Fact]
    [Trait("Requirement", "UX-DR34")]
    [Trait("Requirement", "AD-23")]
    public void Only_named_types_read_an_Accounts_email_address()
    {
        var readers = AccountReaders(RuntimeAssemblies().Select(assembly => assembly.Location), "get_EmailAddress", "get_NormalizedEmailAddress");

        Assert.Contains("Yello.Infrastructure.Persistence.InvitationStore", readers);

        var offenders = readers.Where(reader => !AddressReaders.Contains(reader, StringComparer.Ordinal)).ToList();

        Assert.True(offenders.Count == 0, "These read an Account's address and are not named (AC2, AD-23): " + string.Join(", ", offenders) + ". Readers found: " + string.Join(", ", readers));
    }

    /// <summary>
    /// AC7, UX-DR34, Lee's Q5 (a) - attribution never reads a live Account name: only named types read
    /// <c>Account.DisplayName</c> - the entity and its configuration, the Account-scoped registration slice and store, the
    /// Invitation store (the issue captures <c>IssuerName</c> at authoring time), and 4.4's Membership store (current Members
    /// in Membership management only). The reason, stated (<c>EXPERIENCE.md:156</c>): resolving a current name globally would
    /// propagate a later name change into a Space the reader no longer shares with that person - a live read of Account state
    /// by people with no shared Membership. 7.2's description authorship inherits the rule. <c>Neither_the_list_nor_the_revoke_reads_the_issuers_Account</c>
    /// stays green beside it. Green today, left active. Anchor: the Invitation store is found. Detector:
    /// <see cref="The_Account_reader_scan_detects_a_plant"/>.
    /// </summary>
    [Fact]
    [Trait("Requirement", "UX-DR34")]
    public void Attribution_never_reads_a_live_Account_name()
    {
        var readers = AccountReaders(RuntimeAssemblies().Select(assembly => assembly.Location), "get_DisplayName");

        Assert.Contains("Yello.Infrastructure.Persistence.InvitationStore", readers);

        var offenders = readers.Where(reader => !DisplayNameReaders.Contains(reader, StringComparer.Ordinal)).ToList();

        Assert.True(offenders.Count == 0, "These read an Account's display name and are not named (AC7, EXPERIENCE.md:156): " + string.Join(", ", offenders) + ". Readers found: " + string.Join(", ", readers));
    }

    /// <summary>
    /// The detector oracle for both Account reader scans: <see cref="PlantedAccountReader"/>, in this assembly, reads an
    /// Account's address, its normalised address and its display name, and each scan finds this class as the reader.
    /// </summary>
    [Fact]
    [Trait("Requirement", "AD-23")]
    public void The_Account_reader_scan_detects_a_plant()
    {
        var planted = PlantedAccountReader.Read(new Account
        {
            Id = Guid.NewGuid(),
            EmailAddress = "planted@example.test",
            NormalizedEmailAddress = "planted@example.test",
            DisplayName = "Planted",
            CreatedAt = DateTimeOffset.UnixEpoch,
        });

        Assert.Contains("@", planted, StringComparison.Ordinal);

        var here = typeof(RefusalBoundaryGateTests).Assembly.Location;
        var self = typeof(RefusalBoundaryGateTests).FullName!;

        Assert.Contains(self, AccountReaders([here], "get_EmailAddress", "get_NormalizedEmailAddress"));
        Assert.Contains(self, AccountReaders([here], "get_DisplayName"));
    }

    /// <summary>
    /// AC4, Lee's Q1 (a), AD-1, AR-3 - only the pipeline sets the acting authority: the request-scoped holder in
    /// <c>Yello.Application</c> (a type whose name contains <c>Authority</c>) is written - any setter or any method whose name
    /// starts with <c>Set</c> - by <c>ActiveSpaceResolution</c> and nothing else, and the slice receives the Role
    /// <b>sets</b>, never the Role. Anchor: the pipeline is found writing it. Plant 7: a second writer.
    /// </summary>
    [Fact(Skip = AuthorityPending)]
    [Trait("Requirement", "AD-1")]
    [Trait("Requirement", "AR-3")]
    [Trait("Requirement", "FR-13")]
    public void Only_the_pipeline_sets_the_acting_authority()
    {
        var holder = Assert.Single(
            ProductionAssemblies.Application.GetTypes(),
            type => type.Name.Contains("Authority", StringComparison.Ordinal) && !type.IsNested && !type.Name.Contains("RoleChange", StringComparison.Ordinal));
        var writers = InvitationCalls()
            .Where(call => call.DeclaringType == holder.FullName && (call.Name.StartsWith("set_", StringComparison.Ordinal) || call.Name.StartsWith("Set", StringComparison.Ordinal)))
            .Select(call => call.Caller)
            .Where(caller => caller != holder.FullName)
            .ToHashSet(StringComparer.Ordinal);

        Assert.Contains(PipelineTypeName, writers);
        Assert.Equal([PipelineTypeName], writers.Order(StringComparer.Ordinal));
    }

    /// <summary>
    /// AC4, FR-13, PRD section 12 #4, Lee's Q1 (a) - the second transcription, extended with the Qualified cells' target
    /// rules: <c>RoleCapabilityMatrix</c> declares, for each Role, the Roles it may change from and to. Owner: from and to
    /// Admin, Member, Viewer. Admin: from and to Member, Viewer. Member and Viewer: none. <b>Owner is in no set for any
    /// actor.</b> Found by shape: a public static method on <c>RoleCapabilityMatrix</c> taking a <see cref="Role"/> whose answer
    /// has <c>From</c> and <c>To</c> collections of <see cref="Role"/>. Plants 1, 2 and 3.
    /// </summary>
    [Fact(Skip = AuthorityPending)]
    [Trait("Requirement", "FR-13")]
    [Trait("Requirement", "AR-12")]
    [Trait("Assumption", "PRD-12-4")]
    public void The_target_rules_agree_with_the_matrix_transcription()
    {
        (Role Actor, Role[] From, Role[] To)[] transcription =
        [
            (Role.Owner, [Role.Admin, Role.Member, Role.Viewer], [Role.Admin, Role.Member, Role.Viewer]),
            (Role.Admin, [Role.Member, Role.Viewer], [Role.Member, Role.Viewer]),
            (Role.Member, [], []),
            (Role.Viewer, [], []),
        ];

        foreach (var (actor, from, to) in transcription)
        {
            var (declaredFrom, declaredTo) = TargetRuleOf(actor);

            Assert.True(from.Order().SequenceEqual(declaredFrom.Order()), $"{actor} may change from [{string.Join(", ", declaredFrom)}]; the transcription says [{string.Join(", ", from)}].");
            Assert.True(to.Order().SequenceEqual(declaredTo.Order()), $"{actor} may change to [{string.Join(", ", declaredTo)}]; the transcription says [{string.Join(", ", to)}].");
            Assert.DoesNotContain(Role.Owner, declaredFrom.Concat(declaredTo));
        }
    }

    /// <summary>
    /// AC4, Lee's Q1 (a), ledger <c>:329</c> - a Qualified cell reaches no endpoint without its target rule: every mapped route
    /// whose declared capability is Qualified for some Role (today <c>ChangeMembershipRole</c>; 4.5's <c>RemoveMembership</c>
    /// joins it) is served by an endpoint class that depends on the acting-authority holder, through the slice it calls. A
    /// Qualified route added without consuming the authority would let an Admin act on the Admin's whole column. Anchor: at
    /// least one such route is mapped.
    /// </summary>
    [Fact(Skip = AuthorityPending)]
    [Trait("Requirement", "FR-13")]
    [Trait("Requirement", "AD-20")]
    public void A_Qualified_cell_reaches_no_endpoint_without_its_target_rule()
    {
        var qualified = Enum.GetValues<Capability>()
            .Where(capability => Array.Exists(Enum.GetValues<Role>(), role => RoleCapabilityMatrix.Decide(role, capability) == CapabilityDecision.Qualified))
            .ToHashSet();
        var routes = RouteTable()
            .Where(endpoint => endpoint.Metadata.GetMetadata<RequiredCapability>() is { } required && qualified.Contains(required.Capability))
            .ToList();

        Assert.NotEmpty(routes);

        var holder = Assert.Single(
            ProductionAssemblies.Application.GetTypes(),
            type => type.Name.Contains("Authority", StringComparison.Ordinal) && !type.IsNested && !type.Name.Contains("RoleChange", StringComparison.Ordinal));
        var consumers = DependentsOf(holder.FullName!);

        foreach (var capability in routes.Select(endpoint => endpoint.Metadata.GetMetadata<RequiredCapability>()!.Capability).Distinct())
        {
            Assert.True(
                consumers.Any(consumer => consumer.Contains(capability.ToString().Replace("Change", string.Empty, StringComparison.Ordinal).Replace("Remove", string.Empty, StringComparison.Ordinal), StringComparison.Ordinal)),
                $"No slice for the Qualified capability {capability} depends on {holder.FullName}. Its dependents: {string.Join(", ", consumers)}.");
        }
    }

    /// <summary>
    /// AC4, AC5, AR-34, Q10 (a) - <c>owner-role-fixed</c> and <c>role-not-assignable</c> are declared in <c>ProblemTypes</c>
    /// and each has client copy of its own in <c>ClientCopy.resx</c> whose comment names story 4.4. Sibling of 4.2's
    /// <c>The_invitation_not_pending_problem_type_is_declared_and_has_client_copy_naming_story_4_2</c>.
    /// </summary>
    [Fact(Skip = MembershipContractPending)]
    [Trait("Requirement", "AR-34")]
    [Trait("Requirement", "FR-13")]
    public void The_Membership_problem_types_are_declared_and_each_has_client_copy_naming_story_4_4()
    {
        var declared = typeof(ProblemTypes)
            .GetProperties(BindingFlags.Public | BindingFlags.Static)
            .Select(property => property.GetValue(null) as string)
            .ToHashSet(StringComparer.Ordinal);

        var copy = XDocument.Load(Path.Combine(RepositoryLayout.Root.FullName, "Yello.Client", "Localisation", "ClientCopy.resx"))
            .Root!
            .Elements("data")
            .ToDictionary(data => (string)data.Attribute("name")!, data => (string?)data.Element("comment") ?? string.Empty, StringComparer.Ordinal);

        foreach (var code in new[] { "owner-role-fixed", "role-not-assignable" })
        {
            Assert.Contains("urn:yello:problem:" + code, declared);
            Assert.True(copy.TryGetValue(code, out var comment), $"ClientCopy.resx has no entry for {code}.");
            Assert.Contains("4.4", comment, StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// AC1, AC10, UX-DR27 - the client's predicate for showing the Members section agrees with the capability matrix for
    /// every Role: shown exactly where <c>ChangeMembershipRole</c> is not denied (Owner, Admin). Sibling of
    /// <c>The_clients_invite_predicate_agrees_with_the_capability_matrix</c>.
    /// </summary>
    [Fact(Skip = MembershipClientPending)]
    [Trait("Requirement", "UX-DR27")]
    [Trait("Requirement", "UX-DR14")]
    public void The_clients_Members_section_predicate_agrees_with_the_capability_matrix()
    {
        var predicate = ClientRolePredicate("ManageMemberships");

        foreach (var role in Enum.GetValues<Role>())
        {
            var expected = RoleCapabilityMatrix.Decide(role, Capability.ChangeMembershipRole) != CapabilityDecision.Denied;

            Assert.True(
                predicate.Invoke(null, [StableCode.Of(role)]) is bool shown && shown == expected,
                $"The client's Members section predicate disagrees with the matrix for {role}.");
        }
    }

    /// <summary>
    /// AC4, UX-DR11, Q12 (a), PRD section 12 #4 - the client's Role offer, <c>MembershipCapabilities.OfferableRoles(viewerRole,
    /// targetRole)</c>, agrees with the target rules for every (viewer, target) pair: the viewer's <c>To</c> set when the
    /// target is in its <c>From</c> set, otherwise nothing - so an Admin is offered Member and Viewer on a Member's or a
    /// Viewer's row and nothing on an Admin's or the Owner's, the Owner is offered Admin, Member and Viewer on every other
    /// row and nothing on its own, and <c>owner</c> is never offered.
    /// </summary>
    [Fact(Skip = MembershipClientPending)]
    [Trait("Requirement", "UX-DR11")]
    [Trait("Requirement", "FR-13")]
    [Trait("Assumption", "PRD-12-4")]
    public void The_clients_Role_offer_agrees_with_the_target_rules_for_every_pair()
    {
        var offer = ProductionAssemblies.Client.GetTypes()
            .SelectMany(type => type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static))
            .SingleOrDefault(method => method.Name == "OfferableRoles" && method.GetParameters().Length == 2);

        Assert.True(offer is not null, "No static OfferableRoles(viewerRole, targetRole) in Yello.Client.");

        string[] adminTargets = ["member", "viewer"];
        string[] ownerTargets = ["admin", "member", "viewer"];

        foreach (var viewer in Enum.GetValues<Role>())
        {
            foreach (var target in Enum.GetValues<Role>())
            {
                var (viewerCode, targetCode) = (StableCode.Of(viewer), StableCode.Of(target));
                var expected = (viewer, target) switch
                {
                    (Role.Owner, not Role.Owner) => ownerTargets,
                    (Role.Admin, Role.Member or Role.Viewer) => adminTargets,
                    _ => [],
                };
                var offered = Assert.IsAssignableFrom<IEnumerable>(offer.Invoke(null, [viewerCode, targetCode])).Cast<object>().Select(code => code.ToString()).Order(StringComparer.Ordinal);

                Assert.True(expected.SequenceEqual(offered), $"{viewerCode} viewing a {targetCode} row is offered [{string.Join(", ", offered)}]; the target rules say [{string.Join(", ", expected)}].");
            }
        }
    }

    /// <summary>
    /// AC4, UX-DR27 - the controls an Admin lacks are absent, never disabled: neither <c>MembershipSection.razor</c> nor
    /// <c>MembershipRow.razor</c> carries a <c>disabled</c> attribute (an <c>aria-disabled</c> is no better and is banned too).
    /// No such gate existed for the product's other sections; this one covers the Membership section, the first whose
    /// controls differ by the viewer's Role per row. Plant 14: a disabled Role control for an Admin.
    /// </summary>
    [Fact(Skip = MembershipClientPending)]
    [Trait("Requirement", "UX-DR27")]
    public void No_control_in_the_Membership_section_is_disabled()
    {
        foreach (var name in new[] { "MembershipSection.razor", "MembershipRow.razor" })
        {
            var file = new FileInfo(Path.Combine(RepositoryLayout.Root.FullName, "Yello.Client", "Components", name));

            Assert.True(file.Exists, $"{name} does not exist in Yello.Client/Components.");

            var markup = File.ReadAllText(file.FullName);

            Assert.False(DisabledAttribute.IsMatch(markup), $"{name} carries a disabled attribute; a missing capability is absent, never disabled (UX-DR27).");
        }
    }

    /// <summary>
    /// The full names of the given types that declare a public instance string property whose name contains <c>Email</c> or
    /// <c>Address</c>.
    /// </summary>
    private static SortedSet<string> AddressCarriers(IEnumerable<Type> types) =>
        new(
            types
                .Where(type => Array.Exists(type.GetProperties(BindingFlags.Public | BindingFlags.Instance), property =>
                    property.PropertyType == typeof(string)
                    && (property.Name.Contains("Email", StringComparison.Ordinal) || property.Name.Contains("Address", StringComparison.Ordinal))))
                .Select(type => type.FullName!),
            StringComparer.Ordinal);

    /// <summary>
    /// Every type in the given assemblies whose IL calls (or takes the token of) one of <paramref name="getters"/> on
    /// <c>Account</c>, named by its outermost type.
    /// </summary>
    private static SortedSet<string> AccountReaders(IEnumerable<string> locations, params string[] getters)
    {
        var readers = new SortedSet<string>(StringComparer.Ordinal);

        foreach (var location in locations)
        {
            using var module = ModuleDefinition.ReadModule(location);

            foreach (var type in module.GetTypes())
            {
                var reads = type.Methods.Any(method => method.HasBody && method.Body.Instructions.Any(instruction =>
                    instruction.Operand is MethodReference called
                    && getters.Contains(called.Name, StringComparer.Ordinal)
                    && called.DeclaringType.FullName == AccountTypeName));

                if (reads)
                {
                    readers.Add(OutermostOf(type).FullName);
                }
            }
        }

        return readers;
    }

    /// <summary>
    /// The Role sets <c>RoleCapabilityMatrix</c> declares for <paramref name="actor"/>: found by shape (see
    /// <see cref="The_target_rules_agree_with_the_matrix_transcription"/>).
    /// </summary>
    private static (IReadOnlyList<Role> From, IReadOnlyList<Role> To) TargetRuleOf(Role actor)
    {
        var method = typeof(RoleCapabilityMatrix)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .SingleOrDefault(candidate => candidate.GetParameters() is [{ ParameterType: var only }] && only == typeof(Role)
                && candidate.ReturnType.GetProperty("From") is not null && candidate.ReturnType.GetProperty("To") is not null);

        Assert.True(method is not null, "RoleCapabilityMatrix declares no public static method taking a Role whose answer has From and To.");

        var rule = method.Invoke(null, [actor]);

        IReadOnlyList<Role> Read(string name) =>
            rule is null
                ? []
                : [.. Assert.IsAssignableFrom<IEnumerable>(method.ReturnType.GetProperty(name)!.GetValue(rule) ?? Array.Empty<Role>()).Cast<Role>()];

        return (Read("From"), Read("To"));
    }

    [GeneratedRegex(@"(?<![\w-])(aria-)?disabled\b", RegexOptions.IgnoreCase, 1000)]
    private static partial Regex DisabledAttribute { get; }

    /// <summary>
    /// The detector plant for the Account reader scans: reads all three guarded getters.
    /// </summary>
    private static class PlantedAccountReader
    {
        public static string Read(Account account) => account.EmailAddress + account.NormalizedEmailAddress + account.DisplayName;
    }

    /// <summary>
    /// The detector plant for the contract scan: a Presence response that carries an address (UX-DR34's banned case).
    /// </summary>
    /// <param name="DisplayName">A name.</param>
    /// <param name="EmailAddress">An address - the plant.</param>
    private sealed record PlantedPresenceResponse(string DisplayName, string EmailAddress);

    /// <summary>
    /// The contract scan's negative plant: an indicator named for an address, which is no address.
    /// </summary>
    /// <param name="AddressedToYou">The indicator.</param>
    private sealed record PlantedOfferResponse(bool? AddressedToYou);
}
