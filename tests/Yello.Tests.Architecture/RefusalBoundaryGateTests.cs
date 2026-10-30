using System.Collections;
using System.Reflection;
using System.Xml.Linq;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Mono.Cecil;
using Xunit;
using Yello.Application.Spaces;
using Yello.Contracts;
using Yello.Domain.AccessRefusals;
using Yello.Domain.Bounds;
using Yello.Domain.Memberships;

namespace Yello.Tests.Architecture;

/// <summary>
/// Story 1.6's structural gates: the Role capability matrix as data, the NFR-8 bound registry,
/// <c>{spaceId}</c> in every Space-scoped path, the refusal contract's stable types, and the
/// purge that must never run on a timer. AC3, AC5, AC6, AC8, AC9; A-4, A-7, A-9, A-12.
/// </summary>
/// <remarks>
/// <para>
/// <b>Written as red-phase scaffolds by the ATDD run on 2026-10-24, before any of story 1.6
/// existed, and activated by the story.</b> The scaffolds reached the story's types by name through
/// reflection so they could compile first; each lookup is now the typed reference it stood for, so a
/// rename is a compile error. Every gate here was planted; the results are in the story's Dev Agent
/// Record.
/// </para>
/// <para>
/// <b>The matrix and the registry are transcribed here independently</b>, from
/// <c>role-capability-matrix.md:9-28</c> and AC8's table. That is the point of AC3's second clause:
/// a restatement the product's matrix can disagree with. A test that read its expectations from
/// the product's own table would agree with any table at all.
/// </para>
/// <para>
/// <b>Two cases were green before the story and were left active</b>, as standing gates:
/// <see cref="No_endpoint_without_spaceId_in_its_path_reaches_a_Space_scoped_entity"/> (A-4 / AC5)
/// and <see cref="No_production_code_runs_on_a_timer_or_as_a_hosted_service"/> (A-9). Both were
/// planted by the ATDD run; the results are in its checklist.
/// </para>
/// </remarks>
[Trait("Suite", "Architecture")]
[Trait("Priority", "P0")]
[Trait("Requirement", "FR-16")]
[Trait("Requirement", "AR-3")]
[Trait("Requirement", "AR-9")]
[Trait("Requirement", "AR-30")]
[Trait("Requirement", "AD-3")]
[Trait("Requirement", "AD-25")]
[Trait("Requirement", "NFR-8")]
public sealed partial class RefusalBoundaryGateTests
{
    private const string PipelineTypeName = "Yello.Host.Spaces.ActiveSpaceResolution";

    /// <summary>
    /// The one endpoint that reaches a Space-scoped entity without <c>{spaceId}</c>: registration
    /// creates the Personal Space and its default Statuses under an id it has just minted. It
    /// resolves nothing by id, so there is nothing for a boundary refusal to be told apart from.
    /// </summary>
    private const string CreatesItsOwnSpace = "/api/v1/accounts";

    /// <summary>
    /// The second, named by story 3.1 and the AD-24 amendment of 2026-10-29: Space creation writes the new Space's
    /// Owner Membership and default Statuses under an id the server has just minted, on the Space creation unit of
    /// work. Like registration it resolves nothing by id, so no boundary refusal exists here to be told apart from an
    /// ordinary not-found. Matched with its method: <c>GET</c> on the same path is the switcher, which must not reach.
    /// </summary>
    private const string CreatesASpace = "POST /api/v1/spaces";

    private static readonly string MatrixTypeName = typeof(RoleCapabilityMatrix).FullName!;
    private static readonly string RecorderPortName = typeof(IAccessRefusalRecorder).FullName!;
    private static readonly string PresencePortName = typeof(ISpacePresenceRegister).FullName!;
    private static readonly string AccessRefusalTypeName = typeof(AccessRefusal).FullName!;

    /// <summary>
    /// The Ownership-Offer row, which is decided by row identity and never by Role (AD-26 / AR-13).
    /// </summary>
    private const string OfferAcceptance = "AcceptOrDeclineOwnershipOffer";

    private const CapabilityDecision Allowed = CapabilityDecision.Allowed;
    private const CapabilityDecision Denied = CapabilityDecision.Denied;
    private const CapabilityDecision Qualified = CapabilityDecision.Qualified;

    /// <summary>
    /// <c>role-capability-matrix.md:9-28</c>, transcribed by hand: Owner, Admin, Member, Viewer.
    /// </summary>
    private static readonly (string Capability, CapabilityDecision Owner, CapabilityDecision Admin, CapabilityDecision Member, CapabilityDecision Viewer)[] Matrix =
    [
        ("ReadProjectsAndTasks", Allowed, Allowed, Allowed, Allowed),
        ("ManageProjects", Allowed, Allowed, Allowed, Denied),
        ("ManageTasks", Allowed, Allowed, Allowed, Denied),
        ("EditTaskDescriptionCollaboratively", Allowed, Allowed, Allowed, Denied),
        ("AssignTask", Allowed, Allowed, Allowed, Denied),
        ("ConfigureSpaceDefaultStatuses", Allowed, Allowed, Denied, Denied),
        ("ConfigureProjectStatusDelta", Allowed, Allowed, Denied, Denied),
        ("ManageLabels", Allowed, Allowed, Denied, Denied),
        ("IssueAndRevokeInvitations", Allowed, Allowed, Denied, Denied),
        ("ChangeMembershipRole", Allowed, Qualified, Denied, Denied),
        ("RemoveMembership", Allowed, Qualified, Denied, Denied),
        ("RenameSpace", Allowed, Allowed, Denied, Denied),
        ("OfferOwnershipTransfer", Allowed, Denied, Denied, Denied),
        ("DeleteSpace", Allowed, Denied, Denied, Denied),
        ("IssueApiTokenForOneself", Allowed, Allowed, Allowed, Allowed),
    ];

    /// <summary>
    /// AC8's table, transcribed by hand: bound, value, the creation operation that consults it.
    /// </summary>
    private static readonly (string Bound, int Limit, string Operation)[] Bounds =
    [
        ("ConcurrentActiveSessionsPerSpace", 50, "EstablishSessionInSpace"),
        ("ProjectsPerSpace", 50, "CreateProject"),
        ("TasksPerProject", 5000, "CreateTask"),
        ("SpacesPerAccount", 50, "CreateSpace"),
        ("MembershipsPerSpace", 100, "AcceptInvitation"),
        ("ConcurrentEditorsPerTask", 10, "EstablishTaskSyncLease"),
    ];

    /// <summary>
    /// The entities AC5 names, by full name. <c>Label</c> (story 2.4) is named so the gate covers it the
    /// day it is written; every other name must resolve to a Domain type, which
    /// <c>Every_Space_scoped_entity_the_gates_name_resolves_to_a_Domain_type</c> holds. The Task's CLR name is
    /// <c>TaskItem</c> (story 2.2, Q1 (b)).
    /// </summary>
    private static readonly string[] SpaceScopedEntities =
    [
        "Yello.Domain.Statuses.StatusDefinition",
        "Yello.Domain.Tasks.TaskItem",
        "Yello.Domain.Projects.Project",
        "Yello.Domain.Labels.Label",
    ];

    /// <summary>
    /// AC3 - the product's matrix agrees with the transcribed one in every Role x capability cell.
    /// </summary>
    [Fact]
    public void The_capability_matrix_agrees_with_the_transcription_in_every_cell()
    {
        var disagreements = new List<string>();

        foreach (var row in Matrix)
        {
            var value = Enum.Parse<Capability>(row.Capability);

            foreach (var (role, expected) in new[]
            {
                (Role.Owner, row.Owner), (Role.Admin, row.Admin), (Role.Member, row.Member), (Role.Viewer, row.Viewer),
            })
            {
                var actual = RoleCapabilityMatrix.Decide(role, value);

                if (actual != expected)
                {
                    disagreements.Add($"{role} x {row.Capability}: matrix says {actual}, transcription says {expected}");
                }
            }
        }

        Assert.True(
            disagreements.Count == 0,
            "Where a restatement disagrees with the matrix the matrix is correct and the restatement "
            + "is a defect (AC3) - so decide which one this is before changing either:\n"
            + string.Join('\n', disagreements));
    }

    /// <summary>
    /// AC3 - the capability vocabulary has one member per Role-decided row, and nothing else but
    /// the Ownership-Offer row, which AD-26 decides by row identity.
    /// </summary>
    [Fact]
    public void The_capability_vocabulary_is_exactly_the_Role_decided_rows()
    {
        var declared = Enum.GetNames<Capability>().Where(name => name != OfferAcceptance).Order(StringComparer.Ordinal);
        var transcribed = Matrix.Select(row => row.Capability).Order(StringComparer.Ordinal);

        Assert.Equal(transcribed, declared);
    }

    /// <summary>
    /// AC8 / A-7 - the registry declares all six NFR-8 bounds, each with its value and the creation
    /// operation that must consult it, and nothing else.
    /// </summary>
    /// <remarks>
    /// Matched on the entry's property <i>values</i>, not its property names, so the shape of an
    /// entry is dev-story's to choose: an entry matches a row when its values include the bound's
    /// name, its number and its operation's name.
    /// </remarks>
    [Fact]
    public void The_bound_registry_declares_all_six_bounds_with_their_values_and_operations()
    {
        var entries = typeof(BoundRegistry)
            .GetMembers(BindingFlags.Public | BindingFlags.Static)
            .Select(member => member switch
            {
                PropertyInfo property => property.GetValue(null),
                FieldInfo field => field.GetValue(null),
                _ => null,
            })
            .OfType<IEnumerable>()
            .Where(candidate => candidate is not string)
            .SelectMany(candidate => candidate.Cast<object>())
            .ToList();

        Assert.Equal(Bounds.Length, entries.Count);

        var unmatched = Bounds
            .Where(bound => !entries.Exists(entry => Describes(entry, bound)))
            .Select(bound => $"{bound.Bound} = {bound.Limit}, consulted by {bound.Operation}")
            .ToList();

        Assert.True(
            unmatched.Count == 0,
            "The registry is missing, or misstates, these NFR-8 bounds - and an unenforced bound is a "
            + "defect, not a relaxation:\n" + string.Join('\n', unmatched));
    }

    /// <summary>
    /// AC5 / A-4 - no endpoint whose path lacks <c>{spaceId}</c> reaches a <c>Task</c>,
    /// <c>Project</c>, <c>Label</c> or <c>StatusDefinition</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Green today, and a standing gate.</b> The route table is the real one: every public
    /// <c>Map*</c> extension in <c>Yello.Host</c> is mapped onto a throwaway application and its
    /// <see cref="EndpointDataSource"/> read, so a route added tomorrow is covered without anyone
    /// extending a list.
    /// </para>
    /// <para>
    /// <b>Vacuity guard:</b> the statuses endpoint must be found and classified as reaching
    /// <c>StatusDefinition</c>, so a scan that cannot see the one Space-scoped route there is fails
    /// rather than passing.
    /// </para>
    /// </remarks>
    [Fact]
    [Trait("Requirement", "AR-9")]
    [Trait("Assumption", "PRD-12-8")]
    public void No_endpoint_without_spaceId_in_its_path_reaches_a_Space_scoped_entity()
    {
        var endpoints = RouteTable();

        using var graph = new CallGraph();

        var reaching = endpoints
            .Where(endpoint => HandlerReachesSpaceScopedEntity(endpoint, graph))
            .ToList();

        Assert.Contains(reaching, endpoint => CarriesSpaceId(endpoint));

        // The exemption must be found reaching, or it is a hole rather than an exemption - and since
        // registration reaches StatusDefinition only inside the store behind its port, finding it is
        // also the proof that the walk crosses a port.
        Assert.Contains(reaching, endpoint => endpoint.RoutePattern.RawText == CreatesItsOwnSpace);

        // Story 3.1's exemption must be found reaching too, through the same port crossing.
        Assert.Contains(reaching, endpoint => MethodAndPattern(endpoint) == CreatesASpace);

        var offenders = reaching
            .Where(endpoint => !CarriesSpaceId(endpoint))
            .Where(endpoint => endpoint.RoutePattern.RawText != CreatesItsOwnSpace && MethodAndPattern(endpoint) != CreatesASpace)
            .Select(MethodAndPattern)
            .ToList();

        Assert.True(
            offenders.Count == 0,
            $"{string.Join(", ", offenders)} reach a Space-scoped entity without {{spaceId}} in the "
            + "path. A bare-id route is what would make a record-writing boundary refusal separable by "
            + "duration from an ordinary not-found (AC5).");
    }

    /// <summary>
    /// Task 3 - every <c>{spaceId}</c> endpoint declares exactly one required capability, so no
    /// Space-scoped endpoint is ever checked against a default.
    /// </summary>
    [Fact]
    public void Every_spaceId_endpoint_declares_exactly_one_capability()
    {
        var capability = typeof(Capability);

        var spaceScoped = RouteTable().Where(CarriesSpaceId).ToList();

        Assert.NotEmpty(spaceScoped);

        var offenders = spaceScoped
            .Where(endpoint => endpoint.Metadata.Count(item => DeclaresCapability(item, capability)) != 1)
            .Select(endpoint => endpoint.RoutePattern.RawText)
            .ToList();

        Assert.True(
            offenders.Count == 0,
            $"{string.Join(", ", offenders)} declare no capability, or more than one. A Space-scoped "
            + "endpoint fails closed, never defaults to read.");
    }

    /// <summary>
    /// Story 1.7 / AC3 / I-11 (static half) - the switcher endpoint reaches no Space-scoped content:
    /// no Project, Task, Label, Status, presence or refusal record, through anything it calls. It may
    /// reach <c>Membership</c> and <c>Space</c>, and only for identity.
    /// </summary>
    /// <remarks>
    /// Vacuity guard: the same walk must find the statuses endpoint reaching
    /// <c>StatusDefinition</c>, so a walk that reads nothing cannot pass.
    /// </remarks>
    [Fact]
    [Trait("Requirement", "FR-9")]
    [Trait("Requirement", "AR-29")]
    [Trait("Requirement", "AD-24")]
    public void The_switcher_endpoint_reaches_no_Space_scoped_content()
    {
        string[] content =
        [
            .. SpaceScopedEntities,
            "Yello.Domain.Spaces.SpacePresence",
            AccessRefusalTypeName,
        ];

        var endpoints = RouteTable();

        var switcher = endpoints.SingleOrDefault(endpoint => endpoint.RoutePattern.RawText == Yello.Contracts.Spaces.SpaceRoutes.Directory
            && endpoint.Metadata.GetMetadata<Microsoft.AspNetCore.Routing.HttpMethodMetadata>()?.HttpMethods.Contains("GET") == true);

        Assert.True(switcher is not null, "No GET /api/v1/spaces endpoint is mapped.");

        using var graph = new CallGraph();

        Assert.Contains(endpoints, endpoint => CarriesSpaceId(endpoint) && HandlerReaches(endpoint, graph, content));

        Assert.False(
            HandlerReaches(switcher, graph, content),
            "The Space switcher reaches Space-scoped content. AD-24: an Account-scoped surface returns "
            + "Space identity (id, name) and nothing else - no count, no Role, no Status, no record.");
    }

    /// <summary>
    /// Story 1.7 / AD-24 - every endpoint the pipeline establishes an Account-scoped context for names
    /// one of AD-24's surfaces as amended on 2026-10-29 (the two read surfaces, and story 3.1's Space
    /// creation), and the set of such endpoints is exactly the one those stories map.
    /// </summary>
    /// <remarks>
    /// <para>
    /// An Account-scoped endpoint reads across Spaces under the <c>AccountId</c> key, so a third one
    /// is an amendment to AD-24 rather than a code change: adding one fails here until this gate's
    /// expected set is edited, which is where the argument belongs. Account settings is the other
    /// surface AD-24 names; story 1.8 gives it the API Token list and revocation, and 5.4 adds
    /// deletion.
    /// </para>
    /// <para>
    /// Read from the real route table. <b>Vacuity guard:</b> the switcher is found marked, and its
    /// surface is <c>SpaceSwitcher</c>.
    /// </para>
    /// </remarks>
    [Fact]
    [Trait("Requirement", "AR-29")]
    [Trait("Requirement", "AD-24")]
    public void Every_Account_scoped_endpoint_names_an_AD_24_surface()
    {
        var marked = RouteTable()
            .Select(endpoint => (
                Endpoint: endpoint,
                Marker: endpoint.Metadata.FirstOrDefault(item => item.GetType().FullName == AccountScopedMarkerName)))
            .Where(pair => pair.Marker is not null)
            .Select(pair => (
                Route: pair.Endpoint.RoutePattern.RawText,
                Surface: pair.Marker!.GetType().GetProperty("Surface")?.GetValue(pair.Marker)?.ToString()))
            .ToList();

        // Story 3.1 / the AD-24 amendment of 2026-10-29 adds exactly one: Space creation, on the switcher's path.
        (string? Route, string? Surface)[] expected =
        [
            (Yello.Contracts.ApiTokens.ApiTokenRoutes.List, "AccountSettings"),
            (Yello.Contracts.ApiTokens.ApiTokenRoutes.Revoke, "AccountSettings"),
            (Yello.Contracts.Spaces.SpaceRoutes.Directory, "SpaceCreation"),
            (Yello.Contracts.Spaces.SpaceRoutes.Directory, "SpaceSwitcher"),
        ];

        var actual = marked
            .OrderBy(entry => entry.Route, StringComparer.Ordinal)
            .ThenBy(entry => entry.Surface, StringComparer.Ordinal)
            .ToList();

        Assert.True(
            expected.SequenceEqual(actual),
            $"The Account-scoped endpoints are [{string.Join(", ", actual)}], not [{string.Join(", ", expected)}]. "
            + "AD-24 as amended on 2026-10-29 (story 3.1) admits the switcher, Account settings' Token list and "
            + "revocation, and Space creation; another Account-scoped endpoint is a further amendment to AD-24, "
            + "recorded in ARCHITECTURE-SPINE.md, not a code change.");

        Assert.All(marked, entry => Assert.Contains(entry.Surface, Enum.GetNames<Yello.Host.Spaces.AccountScopedSurface>()));
    }

    /// <summary>
    /// Story 1.7 / AD-24 - <c>Space</c>, which carries no row-level security policy, is read only by
    /// the types named here: the switcher's directory (through a join from the policy-protected
    /// <c>Membership</c> read), the active Space's name reader (for the Space resolved on that request),
    /// registration (which creates one), and story 3.1's provisioning and Space store (which create one
    /// and rename the resolved one).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Why a gate rather than a policy.</b> <c>Space</c> stays policy-free by Lee's decision at story
    /// 1.5, pending the AD-24 amendment, so any type reading it could name any Space in the database.
    /// The model configurations and the context refer to the type without reading anything.
    /// </para>
    /// <para>
    /// <b>Vacuity guard:</b> the directory is found among the dependents.
    /// </para>
    /// </remarks>
    [Fact]
    [Trait("Requirement", "AR-29")]
    [Trait("Requirement", "AD-24")]
    public void Space_is_read_only_where_named()
    {
        const string space = "Yello.Domain.Spaces.Space";

        string[] permitted =
        [
            space,
            "Yello.Infrastructure.Persistence.AccountSpaceDirectory",
            "Yello.Infrastructure.Persistence.ActiveSpaceNameReader",
            "Yello.Application.Accounts.RegisterAccount.RegisterAccountHandler",
            "Yello.Domain.Accounts.AccountRegistration",
            "Yello.Infrastructure.Persistence.AccountRegistrationStore",
            "Yello.Infrastructure.Persistence.YelloDbContext",
            "Yello.Infrastructure.Persistence.Configurations.SpaceConfiguration",
            "Yello.Infrastructure.Persistence.Configurations.MembershipConfiguration",
            "Yello.Infrastructure.Persistence.Configurations.StatusDefinitionConfiguration",
            "Yello.Infrastructure.Persistence.Configurations.SpacePresenceConfiguration",
            "Yello.Infrastructure.Persistence.Configurations.ProjectConfiguration",
            "Yello.Infrastructure.Persistence.Configurations.IdempotencyRecordConfiguration",

            // Story 2.4: the Label's foreign key to its Space, declared in the model and reading nothing.
            "Yello.Infrastructure.Persistence.Configurations.LabelConfiguration",

            // Story 3.1: the provisioning both registration and creation build a new Space through, its rows, and the
            // store that writes them and renames the resolved Space - which reads nothing, and whose one update is held
            // to the resolved Space's id by The_only_update_of_a_Space_is_the_rename_of_the_resolved_Space.
            "Yello.Domain.Spaces.NewSpace",
            "Yello.Domain.Spaces.ProvisionedSpace",
            "Yello.Infrastructure.Persistence.SpaceStore",
        ];

        var dependents = DependentsOf(space);

        Assert.Contains("Yello.Infrastructure.Persistence.AccountSpaceDirectory", dependents);

        var offenders = dependents.Where(name => !permitted.Contains(name, StringComparer.Ordinal)).ToList();

        Assert.True(
            offenders.Count == 0,
            $"{string.Join(", ", offenders)} refer to {space}, which carries no policy - so a reader of it "
            + "could name any Space. The switcher reaches Space names only through its Membership join.");
    }

    // Story 1.8 AC9's Token-unreachable gate (CAP-35, AD-24) now lives in RefusalBoundaryGateTests.Move.cs as
    // The_endpoints_unreachable_with_a_Token_are_Token_management_the_switcher_and_the_move: story 2.6 widened it by
    // exactly the move route (FR-35) and kept its vacuity guard.

    /// <summary>
    /// Story 1.8 AC4 / AC10 / NFR-6 - only the Token store and the table's configuration read
    /// <c>ApiToken.SecretHash</c>: nothing else can compare, return or log a Token's hash.
    /// </summary>
    /// <remarks>
    /// Expression-tree reads count (an <c>ldtoken</c> of the getter), which is how the store's lookup
    /// and the configuration reach it. <b>Vacuity guard:</b> the store is found among the readers.
    /// </remarks>
    [Fact]
    [Trait("Requirement", "NFR-6")]
    [Trait("Requirement", "AD-6")]
    public void Only_the_Token_store_and_its_configuration_read_a_Token_hash()
    {
        var readers = RoleReaders("Yello.Domain.ApiTokens.ApiToken", "get_SecretHash");

        Assert.Equal(
            [
                "Yello.Infrastructure.Persistence.ApiTokenStore",
                "Yello.Infrastructure.Persistence.Configurations.ApiTokenConfiguration",
            ],
            readers);
    }

    /// <summary>
    /// Story 1.8 AC6 / AD-4 - only the request establishment, the Token scheme and the anti-forgery
    /// filter depend on the Token principal, so no endpoint, handler or slice can learn which surface
    /// called it.
    /// </summary>
    /// <remarks>
    /// <b>Vacuity guard:</b> the establishment is found among the dependents.
    /// </remarks>
    [Fact]
    [Trait("Requirement", "FR-36")]
    [Trait("Requirement", "AD-4")]
    public void Only_the_establishment_the_Token_scheme_and_anti_forgery_read_the_Token_principal()
    {
        const string principal = "Yello.Host.ApiTokens.ApiTokenPrincipal";

        var dependents = DependentsOf(principal);

        Assert.Equal(
            [
                "Yello.Host.ApiTokens.ApiTokenAuthenticationHandler",
                principal,
                "Yello.Host.Sessions.AntiforgeryRequirement",
                "Yello.Host.Spaces.SpaceRequestEstablishment",
            ],
            dependents);
    }

    private const string SessionOnlyMarkerName = "Yello.Host.ApiTokens.SessionOnlyEndpoint";

    private const string AccountScopedMarkerName = "Yello.Host.Spaces.AccountScopedEndpoint";

    /// <summary>
    /// A-12 - nothing but the pipeline depends on the capability matrix or the refusal recorder.
    /// </summary>
    [Fact]
    [Trait("Priority", "P1")]
    public void Only_the_pipeline_depends_on_the_capability_matrix_and_the_refusal_recorder()
    {
        foreach (var (target, permitted) in new[]
        {
            (MatrixTypeName, new[] { MatrixTypeName, PipelineTypeName }),
            (RecorderPortName, new[]
            {
                RecorderPortName,
                PipelineTypeName,
                "Yello.Infrastructure.Persistence.AccessRefusalRecorder",
                "Yello.Infrastructure.InfrastructureServices",
            }),
            (PresencePortName, new[]
            {
                PresencePortName,
                PipelineTypeName,
                "Yello.Infrastructure.Persistence.SpacePresenceRegister",
                "Yello.Infrastructure.SpaceBoundaryLimits",
            }),
        })
        {
            var dependents = DependentsOf(target);

            Assert.Contains(PipelineTypeName, dependents);

            var offenders = dependents.Where(name => !permitted.Contains(name, StringComparer.Ordinal)).ToList();

            Assert.True(
                offenders.Count == 0,
                $"{string.Join(", ", offenders)} depend on {target}. Authorisation, refusal recording "
                + "and bound checks are the pipeline's (AR-3); a slice that re-implements one is a defect.");
        }
    }

    /// <summary>
    /// A-12, the authorisation clause - nothing but the pipeline reads the Role off the resolved
    /// <see cref="ActiveSpaceContext"/>, so no handler, endpoint or slice decides anything by Role.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A slice that read the Role would be re-implementing the capability check AR-3 puts in the
    /// pipeline - and would be the place a <c>403</c> could quietly become a <c>404</c>, or the
    /// reverse. Read from the IL as a call to the property's getter, so a read through a local, a
    /// lambda or a state machine is seen alike; constructing a context (which the resolver does) is
    /// not a read.
    /// </para>
    /// <para>
    /// <b>Vacuity guard:</b> the pipeline itself reads it, and must be found doing so.
    /// </para>
    /// </remarks>
    [Fact]
    [Trait("Priority", "P1")]
    [Trait("Requirement", "AD-1")]
    public void Nothing_but_the_pipeline_reads_the_Role_off_the_active_Space_context()
    {
        var context = typeof(ActiveSpaceContext).FullName!;
        var getter = "get_" + nameof(ActiveSpaceContext.Role);

        var readers = RoleReaders(context, getter);

        Assert.Contains(PipelineTypeName, readers);

        // The record's own compiler-generated members (Equals, ToString, Deconstruct) read it too.
        // Story 1.7 names one more: the active Space's own answer, which REPORTS the pipeline's
        // resolution to the context bar and decides nothing by Role. It lives in the pipeline's
        // namespace for that reason, and is named here rather than let in by a looser predicate.
        string[] permitted = [PipelineTypeName, context, "Yello.Host.Spaces.ActiveSpaceEndpoints"];

        var offenders = readers.Where(name => !permitted.Contains(name, StringComparer.Ordinal)).ToList();

        Assert.True(
            offenders.Count == 0,
            $"{string.Join(", ", offenders)} read the Role off the active Space context. The pipeline "
            + "checks the endpoint's declared capability against the matrix (AR-3); nothing else may "
            + "decide by Role.");
    }

    /// <summary>
    /// A-12, the authorisation clause, the other way in - no slice, handler or endpoint reads the
    /// Role off a <see cref="Membership"/> row either.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The gate above holds the resolved context. A slice could skip it and read the Membership row
    /// through a reader port instead, which is the same re-implemented capability check. So no type
    /// in <c>Yello.Application</c> or <c>Yello.Host</c> may call <c>Membership.Role</c>'s getter.
    /// Infrastructure may: the resolver is what copies the row's Role into the context, and the
    /// model configuration maps the column.
    /// </para>
    /// <para>
    /// <b>Vacuity guard:</b> the same scan must find the resolver reading it.
    /// </para>
    /// </remarks>
    [Fact]
    [Trait("Priority", "P1")]
    [Trait("Requirement", "AD-1")]
    public void No_slice_or_endpoint_reads_the_Role_off_a_Membership_row()
    {
        var readers = RoleReaders(typeof(Membership).FullName!, "get_" + nameof(Membership.Role));

        Assert.Contains("Yello.Infrastructure.Persistence.MembershipActiveSpaceResolver", readers);

        var offenders = readers
            .Where(name => name.StartsWith("Yello.Application.", StringComparison.Ordinal)
                || name.StartsWith("Yello.Host.", StringComparison.Ordinal))
            .ToList();

        Assert.True(
            offenders.Count == 0,
            $"{string.Join(", ", offenders)} read the Role off a Membership row. The pipeline decides by "
            + "Role (AR-3); a slice that reads it is re-implementing the capability check.");
    }

    /// <summary>
    /// Every production type whose IL calls a property getter on a type, named by its outermost type.
    /// </summary>
    private static SortedSet<string> RoleReaders(string declaringType, string getter)
    {
        var readers = new SortedSet<string>(StringComparer.Ordinal);

        foreach (var assembly in RuntimeAssemblies())
        {
            using var module = ModuleDefinition.ReadModule(assembly.Location);

            foreach (var type in module.GetTypes())
            {
                var reads = type.Methods.Any(method => method.HasBody && method.Body.Instructions.Any(instruction =>
                    instruction.Operand is MethodReference called
                    && called.Name == getter
                    && called.DeclaringType.FullName == declaringType));

                if (reads)
                {
                    readers.Add(OutermostOf(type).FullName);
                }
            }
        }

        return readers;
    }

    /// <summary>
    /// Task 4 - the refusal record is write-only: nothing outside the recorder and the purge refers
    /// to it, so no read surface over it can appear.
    /// </summary>
    [Fact]
    [Trait("Requirement", "AR-11")]
    [Trait("Requirement", "AD-20")]
    public void Only_the_recorder_and_the_purge_refer_to_the_refusal_record()
    {
        string[] permitted =
        [
            AccessRefusalTypeName,
            "Yello.Infrastructure.Persistence.AccessRefusalRecorder",
            "Yello.Infrastructure.Persistence.AccessRefusalPurge",
            "Yello.Infrastructure.Persistence.YelloDbContext",
            "Yello.Infrastructure.Persistence.Configurations.AccessRefusalConfiguration",
        ];

        var dependents = DependentsOf(AccessRefusalTypeName);

        Assert.Contains("Yello.Infrastructure.Persistence.AccessRefusalRecorder", dependents);

        // Migrations and the model snapshot name the table, not the CLR type, so they are not here.
        var offenders = dependents.Where(name => !permitted.Contains(name, StringComparer.Ordinal)).ToList();

        Assert.True(
            offenders.Count == 0,
            $"{string.Join(", ", offenders)} refer to {AccessRefusalTypeName}. The refusal log is "
            + "write-only and has no surface (AD-24).");
    }

    /// <summary>
    /// A-9 - nothing in production runs on a timer or as a hosted service, so the refusal purge
    /// cannot become a schedule that wakes an idle database (AD-10 / AR-33).
    /// </summary>
    /// <remarks>
    /// <b>Green today, and a standing gate.</b> Its vacuity guard is that the same scan does find
    /// <see cref="TimeProvider"/>, which <c>Program.cs</c> registers - so a scanner that reads
    /// nothing fails rather than passing.
    /// </remarks>
    [Fact]
    [Trait("Requirement", "AR-33")]
    [Trait("Requirement", "AD-10")]
    public void No_production_code_runs_on_a_timer_or_as_a_hosted_service()
    {
        string[] banned =
        [
            "Microsoft.Extensions.Hosting.BackgroundService",
            "Microsoft.Extensions.Hosting.IHostedService",
            "Microsoft.Extensions.Hosting.IHostedLifecycleService",
            "System.Threading.PeriodicTimer",
            "System.Threading.Timer",
            "System.Timers.Timer",
        ];

        Assert.Contains(PipelineTypeName, DependentsOf("Microsoft.AspNetCore.Http.EndpointFilterInvocationContext"));
        Assert.NotEmpty(DependentsOf(typeof(TimeProvider).FullName!));

        var offenders = banned
            .SelectMany(name => DependentsOf(name).Select(dependent => $"{dependent} uses {name}"))
            .ToList();

        Assert.True(
            offenders.Count == 0,
            "No component queries the database on a fixed interval (AD-10); the refusal purge is "
            + "triggered by request traffic, at most daily:\n" + string.Join('\n', offenders));
    }

    /// <summary>
    /// AC6 - every refusal the product answers has a stable machine-readable <c>type</c>, and one
    /// <c>not-available</c> type serves every 404.
    /// </summary>
    [Fact]
    [Trait("Requirement", "AR-34")]
    public void The_refusal_problem_types_are_declared_and_one_type_serves_every_404()
    {
        var declared = typeof(ProblemTypes)
            .GetProperties(BindingFlags.Public | BindingFlags.Static)
            .ToDictionary(property => property.Name, property => property.GetValue(null) as string, StringComparer.Ordinal);

        Assert.Equal("urn:yello:problem:not-available", declared.GetValueOrDefault("NotAvailable"));
        Assert.Equal("urn:yello:problem:insufficient-role", declared.GetValueOrDefault("InsufficientRole"));
        Assert.Equal("urn:yello:problem:bound-reached", declared.GetValueOrDefault("BoundReached"));
        Assert.Equal("urn:yello:problem:rate-limited", declared.GetValueOrDefault("RateLimited"));

        Assert.DoesNotContain("SpaceNotFound", declared.Keys);
        Assert.All(declared.Values, value => Assert.StartsWith("urn:yello:problem:", value, StringComparison.Ordinal));
    }

    /// <summary>
    /// AC9 / UX-DR32 / UX-DR35 - the refusal surface's copy is the client's own, in the UX voice,
    /// and the unknown-route page no longer sends anyone back "to where you were".
    /// </summary>
    [Fact]
    [Trait("Requirement", "UX-DR32")]
    [Trait("Requirement", "UX-DR35")]
    public void The_refusal_copy_is_the_clients_own_and_names_no_remembered_location()
    {
        var resx = new FileInfo(Path.Combine(RepositoryLayout.Root.FullName, "Yello.Client", "Localisation", "ClientCopy.resx"));

        var values = XDocument.Load(resx.FullName)
            .Descendants("data")
            .Select(data => data.Element("value")?.Value ?? string.Empty)
            .ToList();

        Assert.Contains("Not available.", values);
        Assert.Contains("Viewers cannot edit Tasks.", values);

        // A remembered location is exactly what UX-DR32 forbids as the route back.
        Assert.DoesNotContain(values, value => value.Contains("where you were", StringComparison.OrdinalIgnoreCase));
    }

    private static bool Describes(object entry, (string Bound, int Limit, string Operation) bound)
    {
        var values = entry.GetType()
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(property => property.GetValue(entry)?.ToString())
            .ToList();

        return values.Contains(bound.Bound, StringComparer.Ordinal)
            && values.Contains(bound.Limit.ToString(System.Globalization.CultureInfo.InvariantCulture), StringComparer.Ordinal)
            && values.Contains(bound.Operation, StringComparer.Ordinal);
    }

    private static bool DeclaresCapability(object metadata, Type capability) =>
        Array.Exists(
            metadata.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance),
            property => property.PropertyType == capability);

    private static bool CarriesSpaceId(RouteEndpoint endpoint) =>
        endpoint.RoutePattern.GetParameter(Yello.Contracts.Spaces.SpaceRoutes.SpaceIdParameter) is not null;

    /// <summary>
    /// An endpoint as "METHOD /pattern", so two endpoints on one path - the switcher and Space creation - are told apart.
    /// </summary>
    private static string MethodAndPattern(RouteEndpoint endpoint) =>
        $"{string.Join(',', endpoint.Metadata.GetMetadata<Microsoft.AspNetCore.Routing.HttpMethodMetadata>()?.HttpMethods ?? [])} {endpoint.RoutePattern.RawText}";

    /// <summary>
    /// Every endpoint the Host maps, built through the framework's own data source.
    /// </summary>
    /// <remarks>
    /// The handlers' parameters are inferred when the data source builds them, and a service type
    /// this throwaway application never registered would be inferred as a request body - which a
    /// <c>GET</c> refuses. So every Yello class and interface outside <c>Yello.Contracts</c> is
    /// registered with a factory that throws; nothing is resolved, only classified. Registering a
    /// replacement <see cref="IServiceProviderIsService"/> instead does not work - the container
    /// answers that service itself and ignores the registration.
    /// </remarks>
    private static List<RouteEndpoint> RouteTable()
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.Services.AddAntiforgery();

        var services = ProductionAssemblies.All
            .Where(assembly => assembly != ProductionAssemblies.Contracts && assembly != ProductionAssemblies.AppHost)
            .SelectMany(assembly => assembly.GetTypes())
            .Where(type => type.Namespace?.StartsWith("Yello.", StringComparison.Ordinal) == true)
            .Where(type => !type.IsGenericTypeDefinition && !type.IsValueType)
            .Where(type => !(type.IsAbstract && type.IsSealed) && !typeof(Delegate).IsAssignableFrom(type));

        foreach (var service in services)
        {
            builder.Services.AddScoped(service, _ => throw new InvalidOperationException("Classified, never resolved."));
        }

        var app = builder.Build();

        var maps = ProductionAssemblies.Host.GetTypes()
            .SelectMany(type => type.GetMethods(BindingFlags.Public | BindingFlags.Static))
            .Where(method => method.Name.StartsWith("Map", StringComparison.Ordinal)
                && method.GetParameters() is [{ ParameterType: var only }]
                && only == typeof(IEndpointRouteBuilder))
            .ToList();

        Assert.NotEmpty(maps);

        foreach (var map in maps)
        {
            map.Invoke(null, [app]);
        }

        IEndpointRouteBuilder routes = app;

        return [.. routes.DataSources
            .SelectMany(source => source.Endpoints)
            .OfType<RouteEndpoint>()];
    }

    /// <summary>
    /// Whether the endpoint's handler reaches a Space-scoped entity, through everything it calls in
    /// production code.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Transitive, not one call deep.</b> A handler that calls a port which returns a DTO reaches
    /// the entity only inside the port's implementation. So the walk follows every call into a
    /// production type: through compiler-generated state machines and lambda closures, and from an
    /// interface or abstract method into every production implementation of it.
    /// </para>
    /// <para>
    /// It over-approximates, as a gate should: an interface call is taken to reach every
    /// implementation. The registration endpoint is the proof that the walk crosses a port - it
    /// reaches <c>StatusDefinition</c> only inside <c>AccountRegistrationStore</c>.
    /// </para>
    /// </remarks>
    private static bool HandlerReachesSpaceScopedEntity(RouteEndpoint endpoint, CallGraph graph) =>
        HandlerReaches(endpoint, graph, SpaceScopedEntities);

    /// <summary>
    /// Whether the endpoint's handler reaches any of <paramref name="entities"/>, by the same
    /// transitive walk. Split out by the story 1.7 ATDD run so the switcher's I-11 gate can walk a
    /// wider list than A-4's.
    /// </summary>
    private static bool HandlerReaches(RouteEndpoint endpoint, CallGraph graph, string[] entities)
    {
        var handler = endpoint.Metadata.GetMetadata<MethodInfo>();

        Assert.True(handler is not null, $"{endpoint.RoutePattern.RawText} carries no handler method.");

        var declaring = graph.TypeNamed(handler.DeclaringType!.FullName!.Replace('+', '/'));

        Assert.True(declaring is not null, $"{endpoint.RoutePattern.RawText}'s handler type was not found in production code.");

        var queue = new Queue<MethodDefinition>();
        var seen = new HashSet<MethodDefinition>();

        void Enqueue(MethodDefinition method)
        {
            if (seen.Add(method))
            {
                queue.Enqueue(method);
            }
        }

        foreach (var method in declaring.Methods.Where(method => method.Name == handler.Name))
        {
            Enqueue(method);
        }

        while (queue.TryDequeue(out var method))
        {
            if (Array.Exists(entities, entity => MethodReferences(method, entity)))
            {
                return true;
            }

            // The state machine an async method is compiled into, and a local function's body.
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
                .OfType<MethodReference>()
                .Select(graph.Resolve)
                .OfType<MethodDefinition>())
            {
                Enqueue(called);
            }
        }

        return false;
    }

    /// <summary>
    /// Every production type, read once, so calls can be resolved without an assembly resolver.
    /// </summary>
    private sealed class CallGraph : IDisposable
    {
        private readonly List<ModuleDefinition> _modules;
        private readonly Dictionary<string, TypeDefinition> _types;

        public CallGraph()
        {
            _modules = [.. RuntimeAssemblies().Select(assembly => ModuleDefinition.ReadModule(assembly.Location))];
            // TryAdd, not ToDictionary: every module has its own <Module> and compiler-generated
            // <PrivateImplementationDetails>, and those are never what a call resolves to.
            _types = new Dictionary<string, TypeDefinition>(StringComparer.Ordinal);

            foreach (var type in _modules.SelectMany(module => module.GetTypes()))
            {
                _types.TryAdd(type.FullName, type);
            }
        }

        public TypeDefinition? TypeNamed(string fullName) => _types.GetValueOrDefault(fullName);

        /// <summary>
        /// The production definition a call names, or <c>null</c> for a call outside production code.
        /// </summary>
        public MethodDefinition? Resolve(MethodReference reference) =>
            TypeNamed(reference.DeclaringType.GetElementType().FullName) is { } type
                ? type.Methods.FirstOrDefault(method =>
                    method.Name == reference.Name && method.Parameters.Count == reference.Parameters.Count)
                : null;

        /// <summary>
        /// Every production method that implements or overrides an interface or abstract method,
        /// matched by name and arity - an over-approximation, which is the safe direction for a gate.
        /// </summary>
        public IEnumerable<MethodDefinition> ImplementationsOf(MethodDefinition method)
        {
            if (!method.IsAbstract)
            {
                return [];
            }

            var owner = method.DeclaringType.FullName;

            return _types.Values
                .Where(type => type.Interfaces.Any(implemented => implemented.InterfaceType.GetElementType().FullName == owner)
                    || type.BaseType?.GetElementType().FullName == owner)
                .SelectMany(type => type.Methods)
                .Where(candidate => !candidate.IsAbstract
                    && (candidate.Name == method.Name || candidate.Name.EndsWith("." + method.Name, StringComparison.Ordinal))
                    && candidate.Parameters.Count == method.Parameters.Count);
        }

        public void Dispose()
        {
            foreach (var module in _modules)
            {
                module.Dispose();
            }
        }
    }

    private static SortedSet<string> DependentsOf(string fullName)
    {
        var dependents = new SortedSet<string>(StringComparer.Ordinal);

        foreach (var assembly in RuntimeAssemblies())
        {
            using var module = ModuleDefinition.ReadModule(assembly.Location);

            foreach (var type in module.GetTypes().Where(type => TypeReferences(type, fullName)))
            {
                dependents.Add(OutermostOf(type).FullName);
            }
        }

        return dependents;
    }

    /// <summary>
    /// The assemblies that run in production. The AppHost orchestrates local development and is
    /// not one of them.
    /// </summary>
    private static IEnumerable<Assembly> RuntimeAssemblies() =>
        ProductionAssemblies.All.Where(assembly => assembly != ProductionAssemblies.AppHost);

    private static bool Names(TypeReference? candidate, string fullName) => candidate switch
    {
        null => false,
        _ when candidate.FullName == fullName => true,
        GenericInstanceType generic when generic.GenericArguments.Any(argument => Names(argument, fullName)) => true,
        TypeSpecification specification => Names(specification.ElementType, fullName),
        _ => false,
    };

    private static bool TypeReferences(TypeDefinition type, string fullName) =>
        Names(type.BaseType, fullName)
        || type.Interfaces.Any(implemented => Names(implemented.InterfaceType, fullName))
        || type.Fields.Any(field => Names(field.FieldType, fullName))
        || type.Methods.Any(method => MethodReferences(method, fullName));

    private static bool MethodReferences(MethodDefinition method, string fullName)
    {
        bool InOperand(object? operand) => operand switch
        {
            GenericInstanceMethod generic => Names(generic.DeclaringType, fullName)
                || Names(generic.ReturnType, fullName)
                || generic.GenericArguments.Any(argument => Names(argument, fullName)),
            MethodReference reference => Names(reference.DeclaringType, fullName)
                || Names(reference.ReturnType, fullName)
                || reference.Parameters.Any(parameter => Names(parameter.ParameterType, fullName)),
            FieldReference field => Names(field.FieldType, fullName) || Names(field.DeclaringType, fullName),
            TypeReference reference => Names(reference, fullName),
            _ => false,
        };

        bool InBody() =>
            method.HasBody
            && (method.Body.Variables.Any(variable => Names(variable.VariableType, fullName))
                || method.Body.Instructions.Any(instruction => InOperand(instruction.Operand)));

        return Names(method.ReturnType, fullName)
            || method.Parameters.Any(parameter => Names(parameter.ParameterType, fullName))
            || InBody();
    }

    private static TypeDefinition OutermostOf(TypeDefinition type) =>
        type.DeclaringType is null ? type : OutermostOf(type.DeclaringType);
}
