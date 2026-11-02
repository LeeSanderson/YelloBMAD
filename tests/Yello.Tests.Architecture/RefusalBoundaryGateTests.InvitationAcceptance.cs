using System.Reflection;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.AspNetCore.Routing;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Xunit;
using Yello.Contracts;
using Yello.Domain.Bounds;
using Yello.Host.Spaces;

namespace Yello.Tests.Architecture;

/// <summary>
/// Story 4.3's structural gates for viewing and accepting an Invitation: the two routes and their credentials and bound
/// (AC4, AC9, Lee's Q4 (a), Q7 (a)); the view's and the accept's contracts (AC2, AC4, Q10 (a)); the accept as the Invitation
/// store's third sanctioned state write (AC12, A-10, ledger <c>:723</c>); no raw SQL naming an Invitation table outside the
/// store (AC12, Q18 (a), ledger <c>:743</c>); the token index's one reader (AC12, Lee's Q1 (a)); the acceptance unit of
/// work's dependents (AC12, ledger <c>:670</c>); the one Account-constructing slice and an acceptance slice that never
/// provisions (AC3, AD-22, plant 17); and the new problem types with their copy (AC6, AC7). Written by the story 4.3 ATDD
/// run, 2026-11-01.
/// </summary>
/// <remarks>
/// <para>
/// <b>Written as red-phase scaffolds, activated by the dev phase.</b> Each gate finds its subject by name, from the story's
/// Project Structure Notes: <c>Yello.Contracts.Invitations.InvitationOfferResponse</c>, <c>AcceptInvitationRequest</c> and
/// <c>AcceptedInvitationResponse</c>; <c>AcceptInvitationHandler</c> in <c>Yello.Application</c>;
/// <c>Yello.Infrastructure.Persistence.InvitationStore</c>; a type named <c>InvitationToken</c> (Lee's Q1 (a)); the acceptance
/// unit of work found as the one new <c>SpaceId</c>-key setter whose name contains <c>Invitation</c> (Lee's Q2 (a); its
/// name, e.g. <c>InvitationScope</c>, is the dev phase's). A different name is corrected here first and recorded in the
/// Debug Log. The route patterns are literals, as in 4.2's file: they are what the gate asserts.
/// </para>
/// <para>
/// <b>Left active and green, by design (standing regression gates):</b>
/// <see cref="Only_the_registration_slice_constructs_an_Account"/> (AD-22; its detector is proved by
/// <see cref="The_Account_construction_scan_detects_a_plant"/>) and
/// <see cref="No_raw_SQL_names_an_Invitation_table_outside_the_Invitation_store"/> (Q18 (a); its detector is proved by
/// <see cref="The_raw_SQL_scan_detects_a_plant"/>). Both hold today and must keep holding once 4.3 lands.
/// </para>
/// <para>
/// <b>Unchanged and still the guard:</b> 4.2's <c>Only_the_Invitation_store_writes_an_Invitation_state</c> (its bulk-write
/// set is a set of <c>Caller.Method</c> names, so an accept in <c>InvitationStore</c> keeps it green and an accept anywhere
/// else turns it red - plant 13), <c>The_Invitation_routes_all_require_IssueAndRevokeInvitations_and_serve_both_surfaces</c>
/// (its <c>RequiredBound</c> clause is AC9's "issue consults no bound"), 4.1's
/// <c>Only_the_Invitation_store_and_its_configuration_read_an_Invitation_token_hash</c> and
/// <c>Only_the_shared_actionable_predicate_compares_ExpiresAt</c> (plant 14), and
/// <c>No_production_code_runs_on_a_timer_or_as_a_hosted_service</c>.
/// </para>
/// </remarks>
public sealed partial class RefusalBoundaryGateTests
{
    /// <summary>
    /// TEMPORARY stand-in patterns (Lee's Q4 (a)), as the story names them; the dev phase points these at
    /// <c>InvitationRoutes</c> once Task 3 declares the routes, or keeps them as the literals the gate asserts.
    /// </summary>
    private const string OfferPattern = "/api/v1/invitations/offer";

    private const string AcceptancePattern = "/api/v1/invitations/acceptance";

    private const string SpaceSessionContextName = "Yello.Infrastructure.Persistence.SpaceSessionContext";

    private static readonly string[] SecretPropertyNames = ["Token", "Secret", "Plaintext"];

    /// <summary>
    /// AC4, AC9, AD-4, AD-25, Lee's Q4 (a) and Q7 (a) - the view is exactly <c>GET</c> on its path and the accept exactly
    /// <c>POST</c> on its; neither carries <c>{spaceId}</c> nor declares a Space capability (no Role is held by an invitee);
    /// the accept is Session-only (a Token is the no-credential <c>401</c>) and consults
    /// <see cref="Bound.MembershipsPerSpace"/>; the view consults no bound. Plant 11: the accept without the Session-only
    /// marker.
    /// </summary>
    [Fact(Skip = "red: AC4 / AC9 - the view and accept routes are not mapped; Task 6, Lee's Q4 (a) and Q7 (a)")]
    [Trait("Requirement", "FR-11")]
    [Trait("Requirement", "AD-4")]
    [Trait("Requirement", "AR-30")]
    [Trait("Requirement", "AD-25")]
    public void The_view_and_accept_routes_carry_no_Space_the_accept_is_Session_only_and_consults_the_Memberships_bound()
    {
        var routes = RouteTable()
            .Where(endpoint => endpoint.RoutePattern.RawText is OfferPattern or AcceptancePattern)
            .SelectMany(endpoint => (endpoint.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods ?? [])
                .Select(method => (Key: $"{method} {endpoint.RoutePattern.RawText}", Endpoint: endpoint)))
            .ToDictionary(route => route.Key, route => route.Endpoint, StringComparer.Ordinal);

        Assert.Equal([$"GET {OfferPattern}", $"POST {AcceptancePattern}"], routes.Keys.Order(StringComparer.Ordinal));
        Assert.All(routes.Values, endpoint => Assert.Null(endpoint.Metadata.GetMetadata<RequiredCapability>()));

        var accept = routes[$"POST {AcceptancePattern}"];
        var view = routes[$"GET {OfferPattern}"];

        Assert.Contains(accept.Metadata, metadata => metadata.GetType().Name == "SessionOnlyEndpoint");
        Assert.Equal(Bound.MembershipsPerSpace, accept.Metadata.GetMetadata<RequiredBound>()?.Bound);
        Assert.Null(view.Metadata.GetMetadata<RequiredBound>());
    }

    /// <summary>
    /// AC2, UX-DR18, Lee's Q5 (a) - the view's contract, <c>InvitationOfferResponse</c>, carries exactly the Space's name,
    /// the Role's code and the issuer's name (strings) and the addressee indicator (<c>bool?</c>): never the address, an id,
    /// the expiry, the state or a count. Plant 8: a fifth property.
    /// </summary>
    [Fact(Skip = "red: AC2 - InvitationOfferResponse is not declared; Task 3, Lee's Q5 (a)")]
    [Trait("Requirement", "FR-11")]
    [Trait("Requirement", "UX-DR18")]
    [Trait("Requirement", "AD-23")]
    public void The_view_response_carries_the_Space_name_the_Role_the_issuer_and_the_addressee_indicator_and_nothing_else()
    {
        var response = ProductionAssemblies.Contracts.GetType("Yello.Contracts.Invitations.InvitationOfferResponse");

        Assert.True(response is not null, "Yello.Contracts.Invitations.InvitationOfferResponse is not declared.");
        Assert.Equal(["AddressedToYou", "IssuerName", "Role", "SpaceName"], PropertyNames(response));
        Assert.Equal(typeof(bool?), response.GetProperty("AddressedToYou")?.PropertyType);

        foreach (var name in new[] { "SpaceName", "Role", "IssuerName" })
        {
            Assert.Equal(typeof(string), response.GetProperty(name)?.PropertyType);
        }
    }

    /// <summary>
    /// AC4, Lee's Q4 (a), Q10 (a), AD-28 - the accept's request carries the token and nothing else (the Role and the Space
    /// come from the row, never the body), and its property is not named <c>Token</c>, <c>Secret</c> or <c>Plaintext</c>
    /// (<c>ApiTokenGateTests.Only_the_issuance_response_carries_a_Token_secret</c> matches names exactly); the accept's answer
    /// carries the Space's id and nothing else.
    /// </summary>
    [Fact(Skip = "red: AC4 - AcceptInvitationRequest and AcceptedInvitationResponse are not declared; Task 3")]
    [Trait("Requirement", "FR-11")]
    [Trait("Requirement", "AD-28")]
    public void The_accept_request_carries_the_token_alone_and_its_answer_the_Space_alone()
    {
        var request = ProductionAssemblies.Contracts.GetType("Yello.Contracts.Invitations.AcceptInvitationRequest");
        var accepted = ProductionAssemblies.Contracts.GetType("Yello.Contracts.Invitations.AcceptedInvitationResponse");

        Assert.True(request is not null, "Yello.Contracts.Invitations.AcceptInvitationRequest is not declared.");
        Assert.True(accepted is not null, "Yello.Contracts.Invitations.AcceptedInvitationResponse is not declared.");

        var only = Assert.Single(request.GetProperties(BindingFlags.Public | BindingFlags.Instance));

        Assert.Equal(typeof(string), only.PropertyType);
        Assert.DoesNotContain(only.Name, SecretPropertyNames);
        Assert.Equal(["SpaceId"], PropertyNames(accepted));
        Assert.Equal(typeof(Guid), accepted.GetProperty("SpaceId")?.PropertyType);
    }

    /// <summary>
    /// AC5, AC12, A-10, AD-27, AD-28, ledger <c>:723</c> - the accept is the Invitation store's third sanctioned state write
    /// (after the issue's insert of <c>Pending</c> and 4.2's revoke): <c>InvitationStore</c> declares an accept whose body -
    /// its async state machine included - runs <c>ExecuteUpdateAsync</c> over <c>Invitation</c> (the one guarded statement,
    /// never load-check-save, plant 3) and inserts a <c>Membership</c>. 4.2's gate keeps every other writer out.
    /// </summary>
    [Fact(Skip = "red: AC5 / AC12 - InvitationStore has no accept; Task 4")]
    [Trait("Requirement", "AR-31")]
    [Trait("Requirement", "AD-27")]
    [Trait("Requirement", "AR-32")]
    public void The_accept_is_the_Invitation_stores_third_sanctioned_state_write()
    {
        using var module = ModuleDefinition.ReadModule(ProductionAssemblies.Infrastructure.Location);

        var store = Assert.Single(module.Types, type => type.FullName == "Yello.Infrastructure.Persistence.InvitationStore");
        var accept = store.NestedTypes.Where(nested => nested.Name.Contains("Accept", StringComparison.Ordinal))
            .SelectMany(nested => nested.Methods)
            .Concat(store.Methods.Where(method => method.Name.Contains("Accept", StringComparison.Ordinal)))
            .Where(method => method.HasBody)
            .SelectMany(method => method.Body.Instructions)
            .Select(instruction => instruction.Operand)
            .OfType<MethodReference>()
            .ToList();

        Assert.NotEmpty(accept);
        Assert.Contains(accept, called => called is GenericInstanceMethod generic
            && called.Name == "ExecuteUpdateAsync"
            && generic.GenericArguments.Any(argument => argument.FullName == InvitationTypeName));
        Assert.Contains(accept, called => called.Name is "Add" or "AddAsync" && called.DeclaringType.FullName.Contains("Membership", StringComparison.Ordinal));
    }

    /// <summary>
    /// AC12, Q18 (a), ledger <c>:743</c> - no raw SQL in production names an Invitation table (<c>Invitation</c>, and the
    /// token index Lee's Q1 (a) adds) except in the Invitation store: the IL gates see IL calls, this sees the strings a
    /// <c>ExecuteSql*</c>, <c>SqlQuery</c> or <c>FromSql</c> file carries. What neither sees, stated: an expression-tree
    /// filter inside the store. Anchor: the scan reads raw-SQL files (the application locks are one). Standing gate: green
    /// today.
    /// </summary>
    [Fact]
    [Trait("Requirement", "AR-31")]
    [Trait("Requirement", "AD-2")]
    public void No_raw_SQL_names_an_Invitation_table_outside_the_Invitation_store()
    {
        var files = ProductionSources("Yello.Application", "Yello.Infrastructure", "Yello.Host")
            .Where(file => RawSqlCall.IsMatch(StripComments(File.ReadAllText(file))))
            .ToList();

        Assert.Contains(files, file => Path.GetFileName(file) == "ApplicationLocks.cs");

        var offenders = files
            .Where(file => Path.GetFileName(file) != "InvitationStore.cs" && NamesAnInvitationTable(File.ReadAllText(file)))
            .ToList();

        Assert.True(offenders.Count == 0, "Raw SQL names an Invitation table outside InvitationStore:\n" + string.Join('\n', offenders));
    }

    /// <summary>
    /// The detector behind <see cref="No_raw_SQL_names_an_Invitation_table_outside_the_Invitation_store"/>, fired on planted
    /// text: a statement on the table or the token index is seen; the lock's resource name, which merely says
    /// "Invitations", is not.
    /// </summary>
    [Fact]
    public void The_raw_SQL_scan_detects_a_plant()
    {
        Assert.True(NamesAnInvitationTable("""db.Database.ExecuteSqlRawAsync("UPDATE dbo.[Invitation] SET State = N'Accepted'");"""));
        Assert.True(NamesAnInvitationTable("""db.Database.SqlQuery<Guid>($"SELECT OfferedSpaceId FROM dbo.InvitationToken WHERE TokenHash = {hash}");"""));
        Assert.True(NamesAnInvitationTable("""FromSqlRaw("select * from [Invitation] i join Membership m on 1 = 1")"""));
        Assert.False(NamesAnInvitationTable("""internal static string InvitationsOf(Guid spaceId) => $"Yello/Invitations/{spaceId:D}";"""));
    }

    /// <summary>
    /// AC12, Lee's Q1 (a), NFR-6 - the token index (a type named <c>InvitationToken</c>) is policy-free, so it is read where
    /// named and nowhere else: its hash is read by its configuration and exactly one other type, the one reader that
    /// resolves a token to its Space; and nothing beyond the model, the Invitation store and that reader refers to it at
    /// all. Sibling of <c>Space_is_read_only_where_named</c>. The schema half is <c>SchemaVarianceTests.NamedVariances</c>
    /// (<c>InvitationToken.OfferedSpaceId</c>), which fails on its own until the variance is named.
    /// </summary>
    [Fact(Skip = "red: AC12 - the InvitationToken index does not exist; Task 4, Lee's Q1 (a)")]
    [Trait("Requirement", "AD-2")]
    [Trait("Requirement", "NFR-6")]
    public void The_token_index_is_read_by_its_configuration_and_one_named_reader_only()
    {
        var index = Assert.Single(ProductionAssemblies.All.SelectMany(assembly => assembly.GetTypes()), type => type.Name == "InvitationToken");
        var readers = RoleReaders(index.FullName!, "get_TokenHash");
        var configuration = Assert.Single(readers, reader => reader.EndsWith("Configuration", StringComparison.Ordinal));
        var reader = Assert.Single(readers, candidate => candidate != configuration);

        var dependents = DependentsOf(index.FullName!);

        string[] permitted = [index.FullName!, configuration, reader, "Yello.Infrastructure.Persistence.InvitationStore", "Yello.Infrastructure.Persistence.YelloDbContext"];

        var others = dependents
            .Where(dependent => !permitted.Contains(dependent, StringComparer.Ordinal) && !dependent.EndsWith(".InvitationModel", StringComparison.Ordinal))
            .ToList();

        Assert.True(others.Count == 0, "The policy-free token index is reached outside its named readers:\n" + string.Join('\n', others));
    }

    /// <summary>
    /// AC12, ledger <c>:670</c>, Lee's Q2 (a) - the acceptance unit of work (the one <c>SpaceId</c>-key setter whose name
    /// names Invitations) is used where named and nowhere else: its own port, its registration, and the Host's pipeline step
    /// for the view and the accept. No handler, endpoint or other store reaches it, so nothing else runs under a key set from
    /// a token. Modelled on <c>Only_the_pipeline_depends_on_the_capability_matrix_and_the_refusal_recorder</c>.
    /// </summary>
    [Fact(Skip = "red: AC12 - the acceptance unit of work does not exist; Task 4, Lee's Q2 (a)")]
    [Trait("Requirement", "AD-2")]
    [Trait("Requirement", "AD-24")]
    public void The_acceptance_unit_of_work_is_reached_only_by_its_port_its_registration_and_the_pipeline_step()
    {
        var unit = Assert.Single(RoleReaders(SpaceSessionContextName, "SetAsync"), setter => setter.Contains("Invitation", StringComparison.Ordinal));
        var dependents = DependentsOf(unit);

        Assert.Contains(dependents, dependent => dependent.StartsWith("Yello.Host.", StringComparison.Ordinal));

        var others = dependents
            .Where(dependent => dependent != unit && dependent != "Yello.Infrastructure.InfrastructureServices")
            .Where(dependent => !IsTheInvitationPipelineStep(dependent) && !IsAnApplicationPort(dependent))
            .ToList();

        Assert.True(others.Count == 0, $"The acceptance unit of work {unit} is reached outside its pipeline step:\n" + string.Join('\n', others));
    }

    /// <summary>
    /// AC3, AD-22, AR-27 - exactly one slice creates an Account: no production type but
    /// <c>RegisterAccountHandler</c> constructs <c>Account</c>, so acceptance cannot provision a second path. Anchor: the
    /// handler is found constructing it. Standing gate: green today; its detector is proved by
    /// <see cref="The_Account_construction_scan_detects_a_plant"/>.
    /// </summary>
    [Fact]
    [Trait("Requirement", "AR-27")]
    [Trait("Requirement", "AD-22")]
    public void Only_the_registration_slice_constructs_an_Account()
    {
        var constructors = AccountConstructors(RuntimeAssemblies());

        Assert.Equal(["Yello.Application.Accounts.RegisterAccount.RegisterAccountHandler"], constructors);
    }

    /// <summary>
    /// The detector behind <see cref="Only_the_registration_slice_constructs_an_Account"/>, fired on this assembly, which
    /// carries a planted constructor (<see cref="AccountConstructionPlant"/>).
    /// </summary>
    [Fact]
    public void The_Account_construction_scan_detects_a_plant()
    {
        Assert.Contains(typeof(RefusalBoundaryGateTests).FullName!, AccountConstructors([typeof(RefusalBoundaryGateTests).Assembly]));
        Assert.NotNull(AccountConstructionPlant.Construct());
    }

    /// <summary>
    /// AC3, AD-22, plant 17 - the acceptance slice exists and never provisions: <c>AcceptInvitationHandler</c> does not
    /// reach <c>NewSpace</c>, <c>ProvisionedSpace</c>, the registration handler or an outbox (Q14 (a): nothing is published or
    /// mailed on acceptance), and its constructor takes an Invitation port. Sibling of 4.2's
    /// <c>The_revoke_handler_takes_no_outbox</c>.
    /// </summary>
    [Fact(Skip = "red: AC3 - AcceptInvitationHandler does not exist; Task 5, Q14 (a) no outbox or publisher")]
    [Trait("Requirement", "AR-27")]
    [Trait("Requirement", "AD-22")]
    [Trait("Requirement", "AR-3")]
    public void The_acceptance_slice_never_provisions_a_Space_or_an_Account_and_takes_no_outbox()
    {
        var handler = Assert.Single(ProductionAssemblies.Application.GetTypes(), type => type.Name == "AcceptInvitationHandler");
        var parameters = handler.GetConstructors(BindingFlags.Public | BindingFlags.Instance).SelectMany(constructor => constructor.GetParameters()).ToList();

        Assert.Contains(parameters, parameter => parameter.ParameterType.Name.Contains("Invitation", StringComparison.Ordinal));
        Assert.DoesNotContain(parameters, parameter => parameter.ParameterType.Name.Contains("Outbox", StringComparison.Ordinal));

        foreach (var provisioning in new[] { "Yello.Domain.Spaces.NewSpace", "Yello.Domain.Spaces.ProvisionedSpace", "Yello.Application.Accounts.RegisterAccount.RegisterAccountHandler" })
        {
            Assert.DoesNotContain(handler.FullName!, DependentsOf(provisioning));
        }
    }

    /// <summary>
    /// AC6, AC7, AR-34, Lee's Q6 (a), Q13 (a) - <c>invitation-not-valid</c> and <c>invitation-for-another-account</c> are
    /// declared in <c>ProblemTypes</c> and each has client copy of its own in <c>ClientCopy.resx</c> whose comment names story
    /// 4.3; and neither is the boundary's <c>not-available</c>, whose body must stay byte-identical
    /// (<c>RefusalCopyTests.Every_not_found_cause_renders_the_same_refusal</c>). Sibling of 4.2's problem-type gate.
    /// </summary>
    [Fact(Skip = "red: AC6 / AC7 - the two problem types and their copy are not declared; Tasks 3 and 7")]
    [Trait("Requirement", "AR-34")]
    [Trait("Requirement", "FR-11")]
    [Trait("Requirement", "UX-DR18")]
    public void The_invitation_acceptance_problem_types_are_declared_and_each_has_client_copy_naming_story_4_3()
    {
        string[] codes = ["invitation-not-valid", "invitation-for-another-account"];

        var declared = typeof(ProblemTypes)
            .GetProperties(BindingFlags.Public | BindingFlags.Static)
            .Select(property => property.GetValue(null) as string)
            .ToHashSet(StringComparer.Ordinal);

        var copy = XDocument.Load(Path.Combine(RepositoryLayout.Root.FullName, "Yello.Client", "Localisation", "ClientCopy.resx"))
            .Root!
            .Elements("data")
            .ToDictionary(data => (string)data.Attribute("name")!, data => (string?)data.Element("comment") ?? string.Empty, StringComparer.Ordinal);

        foreach (var code in codes)
        {
            Assert.Contains("urn:yello:problem:" + code, declared);
            Assert.NotEqual(ProblemTypes.NotAvailable, "urn:yello:problem:" + code);
            Assert.True(copy.TryGetValue(code, out var comment), $"ClientCopy.resx has no entry for {code}.");
            Assert.Contains("4.3", comment, StringComparison.Ordinal);
        }
    }

    [GeneratedRegex(@"\b(ExecuteSql\w*|SqlQuery\w*|FromSql\w*)\s*[<(]", RegexOptions.None, 1000)]
    private static partial Regex RawSqlCall { get; }

    [GeneratedRegex(@"(?i)\b(FROM|JOIN|UPDATE|INTO|DELETE)\s+(dbo\.)?\[?Invitation(Token)?\]?(\s|$|\W)", RegexOptions.None, 1000)]
    private static partial Regex InvitationTableStatement { get; }

    private static bool NamesAnInvitationTable(string source) => InvitationTableStatement.IsMatch(StripComments(source));

    /// <summary>
    /// The Host's pipeline step for the view and the accept: in <c>Yello.Host.Spaces</c>, named for Invitations or the
    /// <c>ActiveSpaceResolution</c> class it nests in (the <c>Creation.cs</c> model).
    /// </summary>
    private static bool IsTheInvitationPipelineStep(string dependent) =>
        dependent.StartsWith("Yello.Host.Spaces.", StringComparison.Ordinal)
        && (dependent.Contains("Invitation", StringComparison.Ordinal) || dependent.EndsWith(".ActiveSpaceResolution", StringComparison.Ordinal));

    /// <summary>
    /// An interface the unit implements, declared in <c>Yello.Application</c> (its port).
    /// </summary>
    private static bool IsAnApplicationPort(string dependent) =>
        dependent.StartsWith("Yello.Application.", StringComparison.Ordinal) && dependent.Split('.')[^1].StartsWith('I');

    /// <summary>
    /// Every type in <paramref name="assemblies"/> whose IL constructs <c>Yello.Domain.Accounts.Account</c>, by its
    /// outermost type's name.
    /// </summary>
    private static List<string> AccountConstructors(IEnumerable<Assembly> assemblies)
    {
        var constructors = new SortedSet<string>(StringComparer.Ordinal);

        foreach (var assembly in assemblies)
        {
            using var module = ModuleDefinition.ReadModule(assembly.Location);

            foreach (var type in module.GetTypes())
            {
                var constructs = type.Methods.Any(method => method.HasBody && method.Body.Instructions.Any(instruction =>
                    instruction.OpCode == OpCodes.Newobj
                    && instruction.Operand is MethodReference called
                    && called.DeclaringType.FullName == "Yello.Domain.Accounts.Account"));

                if (constructs)
                {
                    constructors.Add(OutermostOf(type).FullName);
                }
            }
        }

        return [.. constructors];
    }

    /// <summary>
    /// The planted Account constructor <see cref="The_Account_construction_scan_detects_a_plant"/> must find.
    /// </summary>
    private static class AccountConstructionPlant
    {
        public static object Construct() =>
            new Yello.Domain.Accounts.Account
            {
                Id = Guid.NewGuid(),
                EmailAddress = "plant@example.test",
                NormalizedEmailAddress = "PLANT@EXAMPLE.TEST",
                DisplayName = "Plant",
                CreatedAt = DateTimeOffset.UnixEpoch,
            };
    }
}
