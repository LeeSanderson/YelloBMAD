using System.Reflection;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Xunit;
using Yello.Contracts;
using Yello.Domain;
using Yello.Domain.Memberships;
using Yello.Host.Spaces;
using Yello.Infrastructure.Persistence;

namespace Yello.Tests.Architecture;

/// <summary>
/// Structural gates for offering ownership of a Space: the three routes and the one capability they share, the offer
/// contracts (no address), the email template's inputs and its outbox kind, the one writer of an offer's state and the
/// revoke's missing outbox, the offer's place in the EF model (Space-scoped, the filtered unique index, the database-clock
/// expiry, no reference but its Space), the shared actionable predicate beside the Invitation's, the problem types and their
/// copy, and the client's Ownership section (Owner-only by the matrix, no browser clock).
/// </summary>
/// <remarks>
/// <para>
/// <b>Each gate finds its subject by name or by shape</b> (route pattern, reflection, Cecil, the EF model, the resource file,
/// the source tree), so a subject built under another name turns it red. The names:
/// <c>Yello.Contracts.Spaces.OfferOwnershipRequest</c> and <c>OwnershipOfferResponse</c>, <c>OwnershipOfferEmail</c>,
/// <c>OutboxKinds.OwnershipOffered</c>, <c>RevokeOwnershipOfferHandler</c>, <c>OwnershipOfferStore</c> and
/// <c>OwnershipOfferWrites</c>, <c>OfferPredicates.OwnershipOfferActionable</c>, the entity <c>OwnershipOffer</c>,
/// <c>Yello.Client/Components/OwnershipSection.razor</c> and a client predicate named <c>PermitsOfferOwnership</c>.
/// </para>
/// <para>
/// <b>Existing gates this work widens deliberately, not here:</b> <c>SpaceScopedEntities</c> gains the entity,
/// <c>Space_is_read_only_where_named</c> gains <c>OwnershipOfferConfiguration</c>, the matrix's route table gains its
/// <c>MatrixRoute</c>, and <c>The_assignment_email_has_its_own_outbox_kind</c>'s sibling below names all three kinds.
/// <b>Kept green unchanged:</b> both Role-read gates, <c>Only_the_shared_actionable_predicate_compares_ExpiresAt</c>,
/// <c>No_production_code_runs_on_a_timer_or_as_a_hosted_service</c>, the address-reader gates, and the two
/// <c>Assert.Single</c> gates over Application types named for "Authority" (no new type may carry that word).
/// </para>
/// </remarks>
public sealed partial class RefusalBoundaryGateTests
{
    private const string OwnershipOffersPattern = "/api/v1/spaces/{spaceId:guid}/ownership-offers";

    private const string PendingOwnershipOfferPattern = "/api/v1/spaces/{spaceId:guid}/ownership-offers/pending";

    private const string OwnershipOfferPattern = "/api/v1/spaces/{spaceId:guid}/ownership-offers/{offerId:guid}";

    private const string OwnershipOfferTypeName = "Yello.Domain.Spaces.OwnershipOffer";

    /// <summary>
    /// The offer routes are exactly <c>POST</c> on the collection, <c>GET</c> on the pending read and <c>DELETE</c> on one offer;
    /// each requires <c>OfferOwnershipTransfer</c>, none is Session-only and none consults a bound, so a Token reaches all three.
    /// </summary>
    [Fact]
    [Trait("Requirement", "FR-8")]
    [Trait("Requirement", "AD-4")]
    [Trait("Requirement", "AD-1")]
    public void The_ownership_offer_routes_require_OfferOwnershipTransfer_and_serve_both_surfaces()
    {
        var routes = RouteTable()
            .Where(endpoint => endpoint.RoutePattern.RawText is OwnershipOffersPattern or PendingOwnershipOfferPattern or OwnershipOfferPattern)
            .SelectMany(endpoint => (endpoint.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods ?? [])
                .Select(method => (Key: $"{method} {endpoint.RoutePattern.RawText}", Endpoint: endpoint)))
            .ToDictionary(route => route.Key, route => route.Endpoint, StringComparer.Ordinal);

        Assert.Equal(
            [$"DELETE {OwnershipOfferPattern}", $"GET {PendingOwnershipOfferPattern}", $"POST {OwnershipOffersPattern}"],
            routes.Keys.Order(StringComparer.Ordinal));
        Assert.All(routes.Values, endpoint => Assert.Equal(Capability.OfferOwnershipTransfer, endpoint.Metadata.GetMetadata<RequiredCapability>()?.Capability));
        Assert.All(routes.Values, endpoint => Assert.DoesNotContain(endpoint.Metadata, metadata => metadata.GetType().Name == "SessionOnlyEndpoint"));
        Assert.All(routes.Values, endpoint => Assert.Null(endpoint.Metadata.GetMetadata<RequiredBound>()));
    }

    /// <summary>
    /// The matrix's "Offer to transfer ownership" row governs exactly the three offer routes: no other route borrows it, and
    /// none of the three is folded under another capability.
    /// </summary>
    [Fact]
    [Trait("Requirement", "FR-8")]
    [Trait("Requirement", "AD-1")]
    public void OfferOwnershipTransfer_is_required_by_exactly_the_three_offer_routes()
    {
        var requiring = RouteTable()
            .Where(endpoint => endpoint.Metadata.GetMetadata<RequiredCapability>()?.Capability == Capability.OfferOwnershipTransfer)
            .SelectMany(endpoint => (endpoint.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods ?? [])
                .Select(method => $"{method} {endpoint.RoutePattern.RawText}"))
            .Order(StringComparer.Ordinal)
            .ToList();

        Assert.Equal([$"DELETE {OwnershipOfferPattern}", $"GET {PendingOwnershipOfferPattern}", $"POST {OwnershipOffersPattern}"], requiring);
    }

    /// <summary>
    /// The offer contracts carry no address, Role, Account or Space: <c>OfferOwnershipRequest</c> is exactly
    /// <c>MembershipId</c> (a <c>Guid?</c>, so a missing one reaches the endpoint's own <c>400</c>), and
    /// <c>OwnershipOfferResponse</c> is exactly <c>Id</c>, <c>RecipientMembershipId</c>, <c>RecipientName</c>,
    /// <c>ExpiresAt</c> and <c>ReadAt</c>.
    /// </summary>
    [Fact]
    [Trait("Requirement", "AD-23")]
    [Trait("Requirement", "FR-8")]
    public void The_ownership_offer_contracts_carry_exactly_their_named_members()
    {
        var request = ProductionAssemblies.Contracts.GetType("Yello.Contracts.Spaces.OfferOwnershipRequest");
        var response = ProductionAssemblies.Contracts.GetType("Yello.Contracts.Spaces.OwnershipOfferResponse");

        Assert.True(request is not null && response is not null, "OfferOwnershipRequest or OwnershipOfferResponse is not declared in Yello.Contracts.Spaces.");
        Assert.Equal(["MembershipId"], PropertyNames(request));
        Assert.Equal(typeof(Guid?), request.GetProperty("MembershipId")?.PropertyType);
        Assert.Equal(["ExpiresAt", "Id", "ReadAt", "RecipientMembershipId", "RecipientName"], PropertyNames(response));
        Assert.Equal(typeof(Guid), response.GetProperty("Id")?.PropertyType);
        Assert.Equal(typeof(Guid), response.GetProperty("RecipientMembershipId")?.PropertyType);
        Assert.Equal(typeof(string), response.GetProperty("RecipientName")?.PropertyType);
        Assert.Equal(typeof(DateTimeOffset), response.GetProperty("ExpiresAt")?.PropertyType);
        Assert.Equal(typeof(DateTimeOffset), response.GetProperty("ReadAt")?.PropertyType);
    }

    /// <summary>
    /// The offer email template's inputs are its content contract: every public constructor and public static method of the
    /// type named <c>OwnershipOfferEmail</c> takes only strings, a URI and a date, and one takes exactly four - the Space's
    /// name, the offerer's name, the link and the expiry. No Domain type can be passed in, so nothing of the Space's contents,
    /// its other Memberships or another Space can reach the email.
    /// </summary>
    [Fact]
    [Trait("Requirement", "FR-43")]
    [Trait("Requirement", "NFR-1")]
    public void The_ownership_offer_email_template_takes_the_Space_the_offerer_the_link_and_the_expiry_and_nothing_else()
    {
        var template = Assert.Single(
            ProductionAssemblies.All.SelectMany(assembly => assembly.GetTypes()),
            type => type.Name == "OwnershipOfferEmail");

        var entries = template.GetConstructors(BindingFlags.Public | BindingFlags.Instance)
            .Cast<MethodBase>()
            .Concat(template.GetMethods(BindingFlags.Public | BindingFlags.Static).Where(method => !method.IsSpecialName && method.DeclaringType == template))
            .ToList();

        Type[] admitted = [typeof(string), typeof(Uri), typeof(DateTimeOffset)];

        Assert.NotEmpty(entries);
        Assert.All(entries, entry => Assert.All(entry.GetParameters(), parameter =>
            Assert.True(admitted.Contains(parameter.ParameterType), $"{template.Name}.{entry.Name} takes {parameter.ParameterType.Name} {parameter.Name}.")));
        Assert.Contains(entries, entry => entry.GetParameters().Select(parameter => parameter.ParameterType).SequenceEqual([typeof(string), typeof(string), typeof(Uri), typeof(DateTimeOffset)]));
        Assert.DoesNotContain(entries, entry => entry.GetParameters().Length > 4);
    }

    /// <summary>
    /// The offer email's kind is a stable code beside the others: <c>OutboxKinds.OwnershipOffered</c> is
    /// <c>ownership-offered</c>, and the three kinds are distinct.
    /// </summary>
    [Fact]
    [Trait("Requirement", "FR-43")]
    [Trait("Requirement", "AR-33")]
    public void The_ownership_offer_email_has_its_own_outbox_kind()
    {
        var kinds = Assert.Single(ProductionAssemblies.All.SelectMany(assembly => assembly.GetTypes()), type => type.Name == "OutboxKinds");

        string? Kind(string name) => kinds.GetProperty(name, BindingFlags.Public | BindingFlags.Static)?.GetValue(null) as string;

        Assert.Equal("ownership-offered", Kind("OwnershipOffered"));
        Assert.Equal(3, new[] { Kind("InvitationIssued"), Kind("TaskAssigned"), Kind("OwnershipOffered") }.Distinct(StringComparer.Ordinal).Count());
    }

    /// <summary>
    /// Only the offer's persistence writes an offer's state: no <c>ExecuteDelete</c> on <c>OwnershipOffer</c> exists anywhere
    /// (offers are retained; a Space's deletion takes them by cascade), the only bulk updates generic over it are
    /// <c>OwnershipOfferStore</c>'s (the revoke, the acceptance and the decline - the three sanctioned transitions a request
    /// names), and only <c>OwnershipOfferStore</c> and <c>OwnershipOfferWrites</c> call
    /// <c>OwnershipOffer.set_State</c>, and only these two write to the table in SQL. Anchor: both types exist in
    /// <c>Yello.Infrastructure.Persistence</c>, and the SQL scan finds both.
    /// </summary>
    [Fact]
    [Trait("Requirement", "AR-31")]
    [Trait("Requirement", "AD-26")]
    [Trait("Requirement", "AD-27")]
    public void Only_the_offer_store_writes_an_OwnershipOffer_state()
    {
        string[] writers = ["Yello.Infrastructure.Persistence.OwnershipOfferStore", "Yello.Infrastructure.Persistence.OwnershipOfferWrites"];
        string[] bulk = ["ExecuteUpdate", "ExecuteUpdateAsync", "ExecuteDelete", "ExecuteDeleteAsync"];

        Assert.All(writers, writer => Assert.NotNull(ProductionAssemblies.Infrastructure.GetType(writer)));
        Assert.NotNull(ProductionAssemblies.Domain.GetType(OwnershipOfferTypeName));

        var calls = InvitationCalls();
        var bulkWrites = calls
            .Where(call => bulk.Contains(call.Name, StringComparer.Ordinal) && call.GenericArguments.Contains(OwnershipOfferTypeName, StringComparer.Ordinal))
            .Select(call => $"{call.Caller}.{call.Name}")
            .ToHashSet(StringComparer.Ordinal);
        var stateSetters = calls
            .Where(call => call.Name == "set_State" && call.DeclaringType == OwnershipOfferTypeName)
            .Select(call => call.Caller)
            .ToHashSet(StringComparer.Ordinal);

        Assert.DoesNotContain(bulkWrites, write => write.EndsWith(".ExecuteDelete", StringComparison.Ordinal) || write.EndsWith(".ExecuteDeleteAsync", StringComparison.Ordinal));
        Assert.Subset(new HashSet<string>([$"{writers[0]}.ExecuteUpdateAsync"], StringComparer.Ordinal), bulkWrites);
        Assert.Subset(writers.ToHashSet(StringComparer.Ordinal), stateSetters);

        // The insert and the lapse are raw SQL, which no method reference shows, so the SQL text is read too. Both writers
        // must be found by it, or the scan would pass on finding nothing.
        var sqlWriters = OwnershipOfferSqlWriters();

        Assert.Equal(writers.ToHashSet(StringComparer.Ordinal), sqlWriters);
    }

    /// <summary>
    /// The outermost type of every production method whose string literals write to <c>OwnershipOffer</c> in SQL.
    /// </summary>
    private static HashSet<string> OwnershipOfferSqlWriters()
    {
        var callers = new HashSet<string>(StringComparer.Ordinal);

        foreach (var assembly in RuntimeAssemblies())
        {
            using var module = Mono.Cecil.ModuleDefinition.ReadModule(assembly.Location);

            foreach (var type in module.GetTypes())
            {
                var writes = type.Methods
                    .Where(method => method.HasBody)
                    .SelectMany(method => method.Body.Instructions)
                    .Any(instruction => instruction.OpCode == Mono.Cecil.Cil.OpCodes.Ldstr
                        && instruction.Operand is string text
                        && OwnershipOfferSqlWrite.IsMatch(text));

                if (writes)
                {
                    callers.Add(OutermostName(type));
                }
            }
        }

        return callers;
    }

    [GeneratedRegex(@"(?i)\b(UPDATE|INSERT\s+INTO|DELETE\s+FROM|DELETE|MERGE(\s+INTO)?)\s+(dbo\.)?\[?OwnershipOffer\]?(\s|$|\W)", RegexOptions.None, 1000)]
    private static partial Regex OwnershipOfferSqlWrite { get; }

    /// <summary>
    /// Nothing is emitted on revoke or lapse because neither can: <c>RevokeOwnershipOfferHandler</c>'s constructors name no
    /// outbox type, and no method of <c>OwnershipOfferWrites</c> takes one. Anchor: the handler exists and takes an offer port.
    /// Sibling of <c>The_revoke_handler_takes_no_outbox</c>.
    /// </summary>
    [Fact]
    [Trait("Requirement", "FR-43")]
    [Trait("Requirement", "AR-33")]
    public void The_ownership_offer_revoke_and_lapse_take_no_outbox()
    {
        var handler = Assert.Single(ProductionAssemblies.Application.GetTypes(), type => type.Name == "RevokeOwnershipOfferHandler");
        var parameters = handler.GetConstructors(BindingFlags.Public | BindingFlags.Instance).SelectMany(constructor => constructor.GetParameters()).ToList();
        var writes = ProductionAssemblies.Infrastructure.GetType("Yello.Infrastructure.Persistence.OwnershipOfferWrites");

        Assert.Contains(parameters, parameter => parameter.ParameterType.Name.Contains("OwnershipOffer", StringComparison.Ordinal));
        Assert.DoesNotContain(parameters, parameter => parameter.ParameterType.Name.Contains("Outbox", StringComparison.Ordinal));
        Assert.NotNull(writes);
        Assert.DoesNotContain(
            writes.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly).SelectMany(method => method.GetParameters()),
            parameter => parameter.ParameterType.Name.Contains("Outbox", StringComparison.Ordinal));
    }

    /// <summary>
    /// The offer in the EF model: Space-scoped (a query filter, a non-nullable <c>SpaceId</c>, an <c>Id</c> never generated by the
    /// database), one foreign key - to <c>Space</c>, cascading - and none to <c>Membership</c> or <c>Account</c>; the filtered
    /// unique index <c>UX_OwnershipOffer_SpaceId_Pending</c> on <c>SpaceId</c> where the state is <c>Pending</c>; <c>State</c>
    /// stored as a string; and <c>ExpiresAt</c> generated on add from a SQL default on the database clock.
    /// </summary>
    [Fact]
    [Trait("Requirement", "AD-2")]
    [Trait("Requirement", "AD-26")]
    [Trait("Requirement", "AR-12")]
    public void The_OwnershipOffer_is_Space_scoped_in_the_model_with_its_filtered_index_and_no_reference_but_its_Space()
    {
        using var context = new YelloDbContextFactory().CreateDbContext([]);

        var offer = context.Model.GetEntityTypes().SingleOrDefault(type => type.ClrType.FullName == OwnershipOfferTypeName);

        Assert.NotNull(offer);
        Assert.NotEmpty(offer.GetDeclaredQueryFilters());
        Assert.False(offer.FindProperty("SpaceId")?.IsNullable ?? true);
        Assert.Equal(Microsoft.EntityFrameworkCore.Metadata.ValueGenerated.Never, offer.FindProperty("Id")?.ValueGenerated);

        var key = Assert.Single(offer.GetForeignKeys());

        Assert.Equal(typeof(Yello.Domain.Spaces.Space), key.PrincipalEntityType.ClrType);
        Assert.Equal(DeleteBehavior.Cascade, key.DeleteBehavior);

        var index = Assert.Single(offer.GetIndexes(), candidate => candidate.GetDatabaseName() == "UX_OwnershipOffer_SpaceId_Pending");

        Assert.True(index.IsUnique);
        Assert.Equal(["SpaceId"], index.Properties.Select(property => property.Name));
        Assert.Contains("Pending", index.GetFilter() ?? string.Empty, StringComparison.Ordinal);
        Assert.Equal(typeof(string), offer.FindProperty("State")?.GetProviderClrType() ?? offer.FindProperty("State")?.ClrType);

        var expiresAt = offer.FindProperty("ExpiresAt");

        Assert.NotNull(expiresAt);
        Assert.Contains("SYSDATETIMEOFFSET", expiresAt.GetDefaultValueSql() ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// The offer's actionable predicate is the shared one, in the one file allowed to compare <c>ExpiresAt</c>:
    /// <c>OfferPredicates</c> declares a static member named <c>OwnershipOfferActionable</c> beside the Invitation's.
    /// </summary>
    [Fact]
    [Trait("Requirement", "AR-31")]
    [Trait("Requirement", "AD-27")]
    public void The_ownership_offer_actionable_predicate_lives_beside_the_Invitations()
    {
        var predicates = Assert.Single(ProductionAssemblies.Infrastructure.GetTypes(), type => type.Name == "OfferPredicates");
        const BindingFlags any = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;

        Assert.True(
            predicates.GetProperty("OwnershipOfferActionable", any) is not null || predicates.GetField("OwnershipOfferActionable", any) is not null || predicates.GetMethod("OwnershipOfferActionable", any) is not null,
            "OfferPredicates declares no OwnershipOfferActionable.");
    }

    /// <summary>
    /// <c>ownership-offer-pending</c>, <c>ownership-offer-not-pending</c> and <c>invalid-offer-recipient</c> are declared in
    /// <c>ProblemTypes</c>, and each has client copy of its own in <c>ClientCopy.resx</c> with a translator's comment.
    /// </summary>
    [Fact]
    [Trait("Requirement", "AR-34")]
    [Trait("Requirement", "FR-8")]
    public void The_ownership_offer_problem_types_are_declared_and_each_has_client_copy()
    {
        var declared = typeof(ProblemTypes)
            .GetProperties(BindingFlags.Public | BindingFlags.Static)
            .Select(property => property.GetValue(null) as string)
            .ToHashSet(StringComparer.Ordinal);

        var copy = XDocument.Load(Path.Combine(RepositoryLayout.Root.FullName, "Yello.Client", "Localisation", "ClientCopy.resx"))
            .Root!
            .Elements("data")
            .ToDictionary(data => (string)data.Attribute("name")!, data => (string?)data.Element("comment") ?? string.Empty, StringComparer.Ordinal);

        foreach (var code in new[] { "ownership-offer-pending", "ownership-offer-not-pending", "invalid-offer-recipient" })
        {
            Assert.Contains("urn:yello:problem:" + code, declared);
            Assert.True(copy.TryGetValue(code, out var comment), $"ClientCopy.resx has no entry for {code}.");
            Assert.False(string.IsNullOrWhiteSpace(comment), $"The {code} copy has no translator's comment.");
        }
    }

    /// <summary>
    /// The client's predicate for showing the Ownership section agrees with the capability matrix for every Role: shown
    /// exactly where <c>OfferOwnershipTransfer</c> is not denied (the Owner), absent for an Admin, a Member and a Viewer.
    /// </summary>
    [Fact]
    [Trait("Requirement", "UX-DR14")]
    [Trait("Requirement", "UX-DR27")]
    [Trait("Requirement", "AD-1")]
    public void The_clients_Ownership_section_predicate_agrees_with_the_capability_matrix()
    {
        var predicate = ClientRolePredicate("PermitsOfferOwnership");

        foreach (var role in Enum.GetValues<Role>())
        {
            var expected = RoleCapabilityMatrix.Decide(role, Capability.OfferOwnershipTransfer) != CapabilityDecision.Denied;

            Assert.True(
                predicate.Invoke(null, [StableCode.Of(role)]) is bool shown && shown == expected,
                $"The client's Ownership predicate disagrees with the matrix for {role}.");
        }
    }

    /// <summary>
    /// The Ownership section counts the days left from the server's own clock: its files exist and none of them reads a
    /// browser clock (<c>DateTime.Now</c>, <c>DateTimeOffset.Now</c>, <c>UtcNow</c> or <c>TimeProvider</c>).
    /// </summary>
    [Fact]
    [Trait("Requirement", "UX-DR14")]
    [Trait("Requirement", "AD-27")]
    public void The_Ownership_section_reads_no_browser_clock()
    {
        var client = Path.Combine(RepositoryLayout.Root.FullName, "Yello.Client");
        var files = Directory.EnumerateFiles(client, "Ownership*", SearchOption.AllDirectories)
            .Where(file => !file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                && !file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .ToList();

        Assert.Contains(files, file => file.EndsWith("OwnershipSection.razor", StringComparison.Ordinal));

        foreach (var file in files)
        {
            var source = File.ReadAllText(file);

            foreach (var clock in new[] { "DateTime.Now", "DateTimeOffset.Now", "UtcNow", "TimeProvider" })
            {
                Assert.False(source.Contains(clock, StringComparison.Ordinal), $"{Path.GetFileName(file)} reads {clock}.");
            }
        }
    }
}
