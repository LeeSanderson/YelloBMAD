using System.Buffers.Text;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Xunit;
using Yello.Contracts;
using Yello.Domain;
using Yello.Domain.Memberships;

namespace Yello.Tests.Architecture;

/// <summary>
/// Story 4.1's structural gates for issuing an Invitation: the request contract (AC1), the entity's fixed Space and Role
/// (AC1), the token secret and who reads its hash (Task 3; NFR-6), the one shared expiry predicate on the database clock
/// (AC8 gates (i) and (ii); AD-27), the email template's content contract (AC7; I-14), who touches the outbox (Lee's Q6),
/// the client's invite predicate and email-rule mirror (AC1, AC3; UX-DR27), the new problem types and their copy (AC2, AC4,
/// AC13), and the log parameters (AC5; I-17). Written by the story 4.1 ATDD run, 2026-11-01.
/// </summary>
/// <remarks>
/// <para>
/// <b>Written as red-phase scaffolds.</b> None of the types exists yet, so every gate finds its subject by name
/// (reflection, Cecil or source) and is skipped with the AC it waits for; on activation a gate may switch to a typed
/// reference. The names are the story's Project Structure Notes: <c>Yello.Domain.Invitations.Invitation</c>,
/// <c>Yello.Contracts.Invitations.IssueInvitationRequest</c>, <c>Yello.Infrastructure.Invitations.InvitationTokenSecret</c>,
/// <c>Yello.Infrastructure.Persistence.InvitationStore</c> and its configuration, a type named <c>InvitationEmail</c>, a
/// type named <c>OutboxMessage</c>, <c>Yello.Domain.Accounts.EmailAddressRule</c>, and the client's
/// <c>InvitationCapabilities.PermitsIssueInvitations</c>. A different name is corrected here first and recorded.
/// </para>
/// <para>
/// <b>Left to the dev phase, because they amend active gates</b> (each must stay green until 4.1 changes the product):
/// <c>SpaceScopedEntities</c> gains <c>Invitation</c>; <c>The_clients_Space_settings_predicate_is_the_union_of_its_sections</c>
/// gains <c>IssueAndRevokeInvitations</c>; <c>No_production_code_runs_on_a_timer_or_as_a_hosted_service</c> is planted
/// with a <c>PeriodicTimer</c> in the dispatcher (plant 5); the AD-19 contract snapshot gains the new contract.
/// </para>
/// </remarks>
public sealed partial class RefusalBoundaryGateTests
{
    private const string InvitationTypeName = "Yello.Domain.Invitations.Invitation";

    private const BindingFlags AnyMember = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;

    /// <summary>
    /// AC1, AD-2, AR-34 - <c>IssueInvitationRequest</c> carries <c>EmailAddress</c> and <c>Role</c>, both strings, and
    /// nothing else: no Space id, no expiry, no token. Sibling of <c>The_Space_request_contracts_carry_a_name_and_nothing_else</c>.
    /// </summary>
    [Fact]
    [Trait("Requirement", "FR-10")]
    [Trait("Requirement", "AD-2")]
    public void The_issue_request_carries_an_address_and_a_Role_and_nothing_else()
    {
        var request = ProductionAssemblies.Contracts.GetType("Yello.Contracts.Invitations.IssueInvitationRequest");

        Assert.True(request is not null, "Yello.Contracts.Invitations.IssueInvitationRequest is not declared.");
        Assert.Equal(["EmailAddress", "Role"], PropertyNames(request));
        Assert.All(request.GetProperties(), property => Assert.Equal(typeof(string), property.PropertyType));
    }

    /// <summary>
    /// AC1, FR-10 - an Invitation's Space and Role are fixed at issue in the type: <c>Invitation.SpaceId</c> and
    /// <c>Invitation.Role</c> have <c>init</c> accessors and no ordinary setter. Sibling of
    /// <c>A_Projects_Space_can_only_be_set_when_it_is_created</c>; the database half is the BLOCK AFTER UPDATE predicate.
    /// </summary>
    [Fact]
    [Trait("Requirement", "FR-10")]
    [Trait("Requirement", "AD-2")]
    public void An_Invitations_Space_and_Role_can_only_be_set_when_it_is_created()
    {
        var invitation = ProductionAssemblies.Domain.GetType(InvitationTypeName);

        Assert.True(invitation is not null, $"{InvitationTypeName} is not declared.");

        foreach (var name in new[] { "SpaceId", "Role" })
        {
            var setter = invitation.GetProperty(name, BindingFlags.Public | BindingFlags.Instance)?.SetMethod;

            Assert.True(setter is not null, $"Invitation.{name} has no setter at all, so it cannot be set at creation either.");
            Assert.Contains(typeof(System.Runtime.CompilerServices.IsExternalInit), setter.ReturnParameter.GetRequiredCustomModifiers());
        }

        Assert.Equal(typeof(Role), invitation.GetProperty("Role")?.PropertyType);
        Assert.Equal(typeof(DateTimeOffset), invitation.GetProperty("ExpiresAt")?.PropertyType);
    }

    /// <summary>
    /// Task 3, NFR-6, C8 - only the Invitation store and the table's configuration read <c>Invitation.TokenHash</c>: nothing
    /// else can compare, return or log it. Sibling of <c>Only_the_Token_store_and_its_configuration_read_a_Token_hash</c>.
    /// </summary>
    /// <remarks>
    /// <b>Corrected by the dev phase (story 4.1):</b> the scaffold required both to read it, but 4.1's store only writes the
    /// hash (an <c>init</c>); story 4.3's lookup is its first reader there. The readers must be a subset of the two named,
    /// and the anchor is the configuration, which reads it today.
    /// </remarks>
    [Fact]
    [Trait("Requirement", "NFR-6")]
    public void Only_the_Invitation_store_and_its_configuration_read_an_Invitation_token_hash()
    {
        var readers = RoleReaders(InvitationTypeName, "get_TokenHash");

        Assert.Contains("Yello.Infrastructure.Persistence.Configurations.InvitationConfiguration", readers);
        Assert.Subset(
            new SortedSet<string>(
                [
                    "Yello.Infrastructure.Persistence.Configurations.InvitationConfiguration",
                    "Yello.Infrastructure.Persistence.InvitationStore",
                ],
                StringComparer.Ordinal),
            readers);
    }

    /// <summary>
    /// Task 3, NFR-6, C8 - an Invitation token is 256 random bits, Base64Url, behind a prefix of its own (not the API
    /// Token's <c>yello_pat_</c>), two are never equal, and the stored hash is SHA-256 of the whole plaintext's UTF-8
    /// bytes. Sibling of <c>A_Token_secret_is_256_random_bits_behind_a_prefix_and_is_stored_as_its_SHA_256</c>; the
    /// methods are found by shape, the prefix by its <c>Prefix</c> constant.
    /// </summary>
    [Fact]
    [Trait("Requirement", "NFR-6")]
    public void An_Invitation_token_is_256_random_bits_behind_its_own_prefix_and_is_stored_as_its_SHA_256()
    {
        var secret = ProductionAssemblies.Infrastructure.GetType("Yello.Infrastructure.Invitations.InvitationTokenSecret");

        Assert.True(secret is not null, "Yello.Infrastructure.Invitations.InvitationTokenSecret does not exist (story 4.1 Task 3).");

        var prefix = Assert.IsType<string>(secret.GetField("Prefix", AnyMember)?.GetValue(null));

        Assert.NotEmpty(prefix);
        Assert.NotEqual("yello_pat_", prefix);
        Assert.False(prefix.StartsWith("yello_pat_", StringComparison.Ordinal) || "yello_pat_".StartsWith(prefix, StringComparison.Ordinal), "The Invitation prefix overlaps the API Token's.");

        var methods = secret.GetMethods(AnyMember).Where(method => method.IsStatic).ToList();
        var generate = Assert.Single(methods, method => method.ReturnType == typeof(string) && method.GetParameters().Length == 0);
        var hash = Assert.Single(methods, method => method.ReturnType == typeof(byte[])
            && method.GetParameters() is [{ ParameterType: var only }] && only == typeof(string));

        var first = Assert.IsType<string>(generate.Invoke(null, null));
        var second = Assert.IsType<string>(generate.Invoke(null, null));

        Assert.NotEqual(first, second);

        foreach (var plaintext in new[] { first, second })
        {
            Assert.StartsWith(prefix, plaintext, StringComparison.Ordinal);
            Assert.Equal(32, Base64Url.DecodeFromChars(plaintext.AsSpan(prefix.Length)).Length);
        }

        Assert.Equal(SHA256.HashData(Encoding.UTF8.GetBytes(first)), Assert.IsType<byte[]>(hash.Invoke(null, [first])));
    }

    /// <summary>
    /// AC8 gate (i), AR-31, AD-27 - exactly one place in <c>Yello.Application</c> and <c>Yello.Infrastructure</c> compares
    /// <c>ExpiresAt</c>: the shared actionable predicate. Allow-listed with a reason: <c>SpacePresenceRegister</c>, whose
    /// presence expiry predates AD-27's Offer/Invitation scope (story 1.6's bound). Comments and migrations are not read.
    /// Anchor: the shared declaration is found, and it compares <c>ExpiresAt</c>. Plant 9: a second <c>ExpiresAt &gt;</c>
    /// in a store.
    /// </summary>
    [Fact]
    [Trait("Requirement", "AR-31")]
    [Trait("Requirement", "AD-27")]
    public void Only_the_shared_actionable_predicate_compares_ExpiresAt()
    {
        string[] allowed = ["SpacePresenceRegister.cs"];

        var comparing = ExpiryComparingFiles();
        var declaration = ActionablePredicateFiles();

        var shared = Assert.Single(declaration);

        Assert.Contains(shared, comparing);

        var others = comparing
            .Where(file => file != shared && !allowed.Contains(Path.GetFileName(file), StringComparer.Ordinal))
            .ToList();

        Assert.True(others.Count == 0, "ExpiresAt is compared outside the one shared predicate (AD-27):\n" + string.Join('\n', others));
    }

    /// <summary>
    /// AC8 gate (ii) (structural half), A-15, AD-27 - the shared predicate reads the database clock: production code maps
    /// <c>SYSDATETIMEOFFSET</c> as a database function, and the predicate's declaration names no C# clock
    /// (<c>UtcNow</c>, <c>GetUtcNow</c>, <c>TimeProvider</c>). The behavioural half - <c>ToQueryString()</c> of a query using
    /// the predicate contains <c>SYSDATETIMEOFFSET</c> - is a Slices test the dev phase writes once the predicate's type
    /// exists (it cannot compile before). Plant 8: the predicate rewritten with <c>clock.GetUtcNow()</c>.
    /// </summary>
    [Fact]
    [Trait("Requirement", "AR-31")]
    [Trait("Requirement", "AD-27")]
    public void The_shared_actionable_predicate_reads_the_database_clock_and_never_a_CSharp_clock()
    {
        var shared = Assert.Single(ActionablePredicateFiles());
        var text = StripComments(File.ReadAllText(shared));

        foreach (var clock in new[] { "UtcNow", "GetUtcNow", "TimeProvider", "DateTime.Now" })
        {
            Assert.DoesNotContain(clock, text, StringComparison.Ordinal);
        }

        Assert.Contains(
            ProductionSources("Yello.Application", "Yello.Infrastructure"),
            file => File.ReadAllText(file).Contains("SYSDATETIMEOFFSET", StringComparison.Ordinal)
                && (File.ReadAllText(file).Contains("DbFunction", StringComparison.Ordinal) || File.ReadAllText(file).Contains("HasDbFunction", StringComparison.Ordinal)));
    }

    /// <summary>
    /// AC7, I-14, FR-39 - the email template's inputs are its content contract: every public constructor and public static
    /// method of the type named <c>InvitationEmail</c> takes only strings, a URI and a date - the Space name, the Role's
    /// display name, the issuer's display name, the link and the expiry - and one takes exactly those five. No Domain type
    /// (Space, Account, Project, Membership) can be passed in, so no Space content can reach the email.
    /// </summary>
    [Fact]
    [Trait("Requirement", "FR-39")]
    [Trait("Requirement", "NFR-1")]
    public void The_invitation_email_template_takes_the_Space_the_Role_the_issuer_the_link_and_the_expiry_and_nothing_else()
    {
        var template = Assert.Single(
            ProductionAssemblies.All.SelectMany(assembly => assembly.GetTypes()),
            type => type.Name == "InvitationEmail");

        var entries = template.GetConstructors(BindingFlags.Public | BindingFlags.Instance)
            .Cast<MethodBase>()
            .Concat(template.GetMethods(BindingFlags.Public | BindingFlags.Static).Where(method => !method.IsSpecialName && method.DeclaringType == template))
            .ToList();

        Type[] admitted = [typeof(string), typeof(Uri), typeof(DateTimeOffset), typeof(DateOnly)];

        Assert.NotEmpty(entries);
        Assert.All(entries, entry => Assert.All(entry.GetParameters(), parameter =>
            Assert.True(admitted.Contains(parameter.ParameterType), $"{template.Name}.{entry.Name} takes {parameter.ParameterType.Name} {parameter.Name}.")));
        Assert.Contains(entries, entry => entry.GetParameters().Length == 5);
        Assert.DoesNotContain(entries, entry => entry.GetParameters().Length > 5);
    }

    /// <summary>
    /// Q6 (Lee), AD-2 variance - only the outbox store, its dispatcher, its configuration and the context refer to the
    /// outbox row type, so a policy-free table holding a Space name and an address is read in exactly the places named.
    /// Sibling of <c>Space_is_read_only_where_named</c>. Anchor: the store and the dispatcher are among the dependents.
    /// </summary>
    [Fact]
    [Trait("Requirement", "AR-33")]
    [Trait("Requirement", "AD-2")]
    public void Only_the_outbox_store_its_dispatcher_and_its_configuration_refer_to_the_outbox_row()
    {
        var row = Assert.Single(ProductionAssemblies.All.SelectMany(assembly => assembly.GetTypes()), type => type.Name == "OutboxMessage");
        var dependents = DependentsOf(row.FullName!);

        // InvitationModel applies the configuration (added by the dev phase: YelloDbContext's S1200 limit moved story 4.1's
        // configurations into it); it reads no row.
        string[] named = ["OutboxStore", "OutboxDispatcher", "OutboxMessageConfiguration", "InvitationModel", "YelloDbContext", "OutboxMessage"];

        Assert.Contains(dependents, dependent => dependent.EndsWith(".OutboxStore", StringComparison.Ordinal));
        Assert.Contains(dependents, dependent => dependent.EndsWith(".OutboxDispatcher", StringComparison.Ordinal));

        var others = dependents.Where(dependent => !named.Contains(dependent.Split('.')[^1], StringComparer.Ordinal)).ToList();

        Assert.True(others.Count == 0, "The outbox row is reached outside the named readers:\n" + string.Join('\n', others));
    }

    /// <summary>
    /// AC1, AC10, UX-DR27 - the client's predicate for showing the invite form agrees with the capability matrix for every
    /// Role: it shows it exactly where <c>IssueAndRevokeInvitations</c> is not denied (Owner, Admin). Sibling of
    /// <c>The_clients_rename_predicate_agrees_with_the_capability_matrix</c>.
    /// </summary>
    [Fact]
    [Trait("Requirement", "UX-DR27")]
    [Trait("Requirement", "FR-10")]
    public void The_clients_invite_predicate_agrees_with_the_capability_matrix()
    {
        var predicate = ClientRolePredicate("IssueInvitations");

        foreach (var role in Enum.GetValues<Role>())
        {
            var expected = RoleCapabilityMatrix.Decide(role, Capability.IssueAndRevokeInvitations) != CapabilityDecision.Denied;

            Assert.True(
                predicate.Invoke(null, [StableCode.Of(role)]) is bool shown && shown == expected,
                $"The client's invite predicate disagrees with the matrix for {role}.");
        }
    }

    /// <summary>
    /// AC3, Task 2 - the address rule is one function shared by registration and invitation (<c>EmailAddressRule</c> in
    /// <c>Yello.Domain.Accounts</c>), and the client's mirror agrees with it on every edge: exactly one <c>@</c>, neither
    /// first nor last, no whitespace, control or Format character, at most 254 characters after trimming. Sibling of the
    /// Space-name rule's client agreement gate.
    /// </summary>
    [Fact]
    [Trait("Requirement", "FR-10")]
    [Trait("Requirement", "FR-1")]
    public void The_Domain_email_rule_and_the_clients_mirror_agree_on_every_edge()
    {
        var domain = PlausibilityPredicate(ProductionAssemblies.Domain.GetType("Yello.Domain.Accounts.EmailAddressRule"));
        var client = PlausibilityPredicate(Assert.Single(ProductionAssemblies.Client.GetTypes(), type => type.Name.Contains("EmailAddressRule", StringComparison.Ordinal)));

        (string Candidate, bool Plausible)[] edges =
        [
            ("person@example.test", true),
            ("  person@example.test  ", true),
            ("ünï@exämple.test", true),
            (new string('x', 254 - "@example.test".Length) + "@example.test", true),
            (new string('x', 255 - "@example.test".Length) + "@example.test", false),
            (string.Empty, false),
            ("   ", false),
            ("no-at-sign", false),
            ("@example.test", false),
            ("person@", false),
            ("two@@example.test", false),
            ("a b@example.test", false),
            ("tab\t@example.test", false),
            ("zero​width@example.test", false),
            ("tag\U000E0041@example.test", false),
            ("lang\U000E0001@example.test", false),
            ("lone\uD800@example.test", false),
            ("smile\U0001F600@example.test", true),
            ("a@" + new string('क़', 252), false),
            ("a@" + new string('क़', 126), true),
        ];

        foreach (var (candidate, plausible) in edges)
        {
            Assert.True(domain(candidate) == plausible, $"The Domain rule answers {!plausible} for '{candidate}'.");
            Assert.True(client(candidate) == plausible, $"The client's mirror answers {!plausible} for '{candidate}'.");
        }
    }

    /// <summary>
    /// AC2, AC3, AC4, AC13, AR-34 - the new refusals have stable machine-readable types: <c>already-a-member</c>,
    /// <c>invitation-pending</c>, <c>role-not-offerable</c> and (assumed) <c>invalid-email-address</c>; and each has client
    /// copy of its own in <c>ClientCopy.resx</c> whose comment names story 4.1.
    /// </summary>
    [Fact]
    [Trait("Requirement", "AR-34")]
    [Trait("Requirement", "FR-10")]
    public void The_invitation_problem_types_are_declared_and_each_has_client_copy_naming_story_4_1()
    {
        string[] codes = ["already-a-member", "invitation-pending", "role-not-offerable", "invalid-email-address"];

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
            Assert.True(copy.TryGetValue(code, out var comment), $"ClientCopy.resx has no entry for {code}.");
            Assert.Contains("4.1", comment, StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// AC5, NFR-6, I-17, Task 6 - no <c>[LoggerMessage]</c> in production takes a parameter named for an address, a
    /// recipient, a token, a link, a subject or a body: the outbox and the issue log the Space and the outbox id only.
    /// Anchor: production declares log messages, so a scan that reads nothing fails.
    /// </summary>
    [Fact]
    [Trait("Requirement", "NFR-6")]
    [Trait("Requirement", "AD-23")]
    public void No_log_message_takes_an_address_a_token_or_an_email_body()
    {
        var messages = ProductionAssemblies.All
            .SelectMany(assembly => assembly.GetTypes())
            .SelectMany(type => type.GetMethods(AnyMember | BindingFlags.DeclaredOnly))
            .Where(method => method.GetCustomAttributes().Any(attribute => attribute.GetType().Name == "LoggerMessageAttribute"))
            .ToList();

        Assert.NotEmpty(messages);

        var offenders = messages
            .SelectMany(method => method.GetParameters().Select(parameter => (method, parameter)))
            .Where(entry => LogBanned.IsMatch(entry.parameter.Name ?? string.Empty))
            .Select(entry => $"{entry.method.DeclaringType?.FullName}.{entry.method.Name}({entry.parameter.Name})")
            .ToList();

        Assert.True(offenders.Count == 0, "A log message takes a value that must never be logged:\n" + string.Join('\n', offenders));
    }

    [GeneratedRegex("(?i)(address|recipient|token|link|subject|body|email)", RegexOptions.None, 1000)]
    private static partial Regex LogBanned { get; }

    [GeneratedRegex(@"\bExpiresAt\s*[<>]|(?<![=\-])[<>]=?\s*[\w.()]*\bExpiresAt\b", RegexOptions.None, 1000)]
    private static partial Regex ExpiryComparison { get; }

    [GeneratedRegex(@"//[^\n]*|/\*.*?\*/", RegexOptions.Singleline, 1000)]
    private static partial Regex Comment { get; }

    private static string StripComments(string source) => Comment.Replace(source, string.Empty);

    /// <summary>
    /// The production C# sources under the named project folders, migrations excluded.
    /// </summary>
    private static List<string> ProductionSources(params string[] projects) =>
        [.. projects
            .Select(project => Path.Combine(RepositoryLayout.Root.FullName, project))
            .Where(Directory.Exists)
            .SelectMany(folder => Directory.EnumerateFiles(folder, "*.cs", SearchOption.AllDirectories))
            .Where(file => !file.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar, StringComparison.Ordinal)
                && !file.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar, StringComparison.Ordinal)
                && !file.Contains(Path.DirectorySeparatorChar + "Migrations" + Path.DirectorySeparatorChar, StringComparison.Ordinal))];

    private static List<string> ExpiryComparingFiles() =>
        [.. ProductionSources("Yello.Application", "Yello.Infrastructure")
            .Where(file => ExpiryComparison.IsMatch(StripComments(File.ReadAllText(file))))];

    /// <summary>
    /// The files that declare the shared actionable predicate: a member named <c>Actionable</c> whose declaration reads
    /// <c>ExpiresAt</c>.
    /// </summary>
    private static List<string> ActionablePredicateFiles() =>
        [.. ProductionSources("Yello.Application", "Yello.Infrastructure", "Yello.Domain")
            .Where(file =>
            {
                var text = StripComments(File.ReadAllText(file));

                return Regex.IsMatch(text, @"\bActionable\b", RegexOptions.None, TimeSpan.FromSeconds(1))
                    && text.Contains("ExpiresAt", StringComparison.Ordinal);
            })];

    /// <summary>
    /// A static <c>bool (string)</c> method on <paramref name="type"/>, as a function.
    /// </summary>
    private static Func<string, bool> PlausibilityPredicate(Type? type)
    {
        Assert.True(type is not null, "The email rule's type is not declared.");

        var method = Assert.Single(
            type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static),
            candidate => candidate.ReturnType == typeof(bool) && candidate.GetParameters() is [{ ParameterType: var only }] && only == typeof(string));

        return candidate => method.Invoke(null, [candidate]) is true;
    }
}
