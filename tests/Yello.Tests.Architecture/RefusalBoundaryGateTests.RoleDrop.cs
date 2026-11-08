using System.Reflection;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Mono.Cecil;
using Xunit;
using Yello.Domain;
using Yello.Domain.Memberships;
using Yello.Host.Sessions;
using Yello.Host.Spaces;

namespace Yello.Tests.Architecture;

/// <summary>
/// Structural gates for a Viewer's Space and a Role that drops: the matrix theory's coverage of every declared capability,
/// the nesting of the Roles' capability sets (and the client's ranks that rely on it), the absence of any Role-shaped
/// disabled state, of any channel shared between tabs and of any client poller, the Role narration's copy, the single
/// writer of the Role header, and the cross-origin policy that lets the client read it.
/// </summary>
/// <remarks>
/// <para>
/// <b>Green today, and standing:</b> the coverage gate, the nesting gate, the disabled gate, the shared-channel gate and the
/// poller gate, each with a planted-signal twin so an empty scan cannot pass. <b>Red until built</b> (skipped, naming the
/// AC): the client ranks, the narration copy, the header writer and the exposed header.
/// </para>
/// <para>
/// <b>Subjects found by name.</b> The client's ranks are a static method <c>RankOf(string)</c> returning an <c>int</c> in
/// <c>Yello.Client</c>; the narration copy is the <c>RoleNarration{Role}</c> entries of <c>ClientCopy.resx</c>; the header is
/// the literal <c>Yello-Role</c> or a getter on a type named <c>RoleHeader</c>. A rename must be made here too.
/// </para>
/// </remarks>
public sealed partial class RefusalBoundaryGateTests
{
    private const string RoleDropRed = "red: AC11 - ";

    private const string RoleHeaderName = "Yello-Role";

    private static readonly Role[] RolesFromHighest = [Role.Owner, Role.Admin, Role.Member, Role.Viewer];

    /// <summary>
    /// The in-flight and data reasons a control may be disabled for, by file: a submission in flight, and the Token form
    /// with no Space to bind a Token to. Anything else bound to <c>disabled</c> or <c>aria-disabled</c> is a new reason, and
    /// a Role reason is never one - a control a Role lacks is absent.
    /// </summary>
    private static readonly Dictionary<string, string[]> PermittedDisabledBindings = new(StringComparer.Ordinal)
    {
        ["FormField.razor"] = ["@Disabled"],
        ["PrimaryButton.razor"] = ["@Disabled"],
        ["AccountSettings.razor"] = ["@_issuing", "@(_issuing || _spaces.Count == 0)"],
        ["InvitationView.razor"] = ["@IsBusy"],
        ["Register.razor"] = ["@IsBusy"],
        ["SignIn.razor"] = ["@IsBusy"],
    };

    /// <summary>
    /// The matrix theory (<c>ActiveSpaceEndpointTests.RoleMatrix.cs</c>, read from source) names exactly the capabilities
    /// some mapped endpoint declares through <c>RequiresCapability</c>: none declared is missing a row, and no row names a
    /// capability no endpoint declares. The first endpoint for a row with none today - Status configuration, an Ownership
    /// Offer, the description editor - fails here until its route joins the theory.
    /// </summary>
    [Fact]
    [Trait("Requirement", "FR-16")]
    [Trait("Requirement", "AR-3")]
    public void The_matrix_theory_covers_exactly_the_capabilities_endpoints_declare()
    {
        var source = new FileInfo(Path.Combine(RepositoryLayout.Root.FullName, "tests", "Yello.Tests.Slices", "Spaces", "ResolveActiveSpace", "ActiveSpaceEndpointTests.RoleMatrix.cs"));

        Assert.True(source.Exists, $"{source.FullName} does not exist, so the matrix theory has no route table to read.");

        var rows = MatrixRouteRow.Matches(File.ReadAllText(source.FullName))
            .Select(match => Enum.Parse<Capability>(match.Groups["capability"].Value))
            .ToList();

        var declared = RouteTable()
            .Select(endpoint => endpoint.Metadata.GetMetadata<RequiredCapability>()?.Capability)
            .OfType<Capability>()
            .Distinct()
            .Order()
            .ToList();

        Assert.Contains(Capability.ReadProjectsAndTasks, rows);
        Assert.Equal(rows.Count, rows.Distinct().Count());
        Assert.Equal(declared, rows.Order().ToList());
    }

    /// <summary>
    /// The Roles' capability sets nest: each Role holds every capability the Role below it holds (<c>Qualified</c> counts as
    /// held), Owner over Admin over Member over Viewer, and each holds at least one the one below does not. A drop can then
    /// be told by rank alone, and "the controls a lower Role lacks" is always a removal, never an addition.
    /// </summary>
    [Fact]
    [Trait("Requirement", "FR-16")]
    [Trait("Requirement", "UX-DR29")]
    public void The_Roles_capability_sets_nest_from_Owner_down_to_Viewer()
    {
        for (var index = 0; index < RolesFromHighest.Length - 1; index++)
        {
            var (higher, lower) = (RolesFromHighest[index], RolesFromHighest[index + 1]);
            var (above, below) = (HeldBy(higher), HeldBy(lower));

            Assert.True(below.IsSubsetOf(above), $"{lower} holds [{string.Join(", ", below.Except(above))}], which {higher} does not.");
            Assert.True(above.IsProperSupersetOf(below), $"{higher} holds nothing {lower} does not, so the two cannot be ranked.");
        }
    }

    /// <summary>
    /// The client's Role ranks, which decide whether a Role change is a drop, agree with the matrix's nesting: for every
    /// pair of Roles, the one ranked higher holds a proper superset of the other's capabilities.
    /// </summary>
    [Fact(Skip = RoleDropRed + "the client has no Role ranks yet")]
    [Trait("Requirement", "UX-DR29")]
    [Trait("Requirement", "FR-16")]
    public void The_clients_Role_ranks_agree_with_the_matrix_nesting()
    {
        var rankOf = ProductionAssemblies.Client.GetTypes()
            .SelectMany(type => type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static))
            .SingleOrDefault(method => method.Name == "RankOf" && method.ReturnType == typeof(int) && method.GetParameters() is [{ ParameterType: var only }] && only == typeof(string));

        Assert.True(rankOf is not null, "No static int RankOf(string roleCode) in Yello.Client.");

        foreach (var higher in RolesFromHighest)
        {
            foreach (var lower in RolesFromHighest)
            {
                var ranked = Assert.IsType<int>(rankOf.Invoke(null, [StableCode.Of(higher)])) > Assert.IsType<int>(rankOf.Invoke(null, [StableCode.Of(lower)]));
                var nests = HeldBy(higher).IsProperSupersetOf(HeldBy(lower));

                Assert.True(ranked == nests, $"The client ranks {StableCode.Of(higher)} above {StableCode.Of(lower)}: {ranked}; the matrix nests it: {nests}.");
            }
        }
    }

    /// <summary>
    /// No client markup binds <c>disabled</c> or <c>aria-disabled</c> for any reason but the in-flight and data ones named
    /// in <see cref="PermittedDisabledBindings"/>, none of those names a capability predicate or a Role, no markup carries an
    /// <c>is-disabled</c> or <c>--disabled</c> class, and no client C# writes either attribute through a render tree. A
    /// control a Role lacks is absent everywhere (UX-DR27).
    /// </summary>
    [Fact]
    [Trait("Requirement", "UX-DR27")]
    [Trait("Requirement", "FR-16")]
    public void No_client_control_is_disabled_for_a_Role()
    {
        var client = new DirectoryInfo(Path.Combine(RepositoryLayout.Root.FullName, "Yello.Client"));
        var markup = ClientSources(client, "*.razor");
        var code = ClientSources(client, "*.cs");

        Assert.NotEmpty(markup);
        Assert.Contains(markup, file => file.Name == "PrimaryButton.razor");

        var offenders = markup
            .SelectMany(file => DisabledBindingsIn(file.Name, File.ReadAllText(file.FullName)))
            .Concat(code.Where(file => RenderTreeDisabled.IsMatch(File.ReadAllText(file.FullName))).Select(file => $"{file.Name} writes a disabled attribute through a render tree"))
            .ToList();

        Assert.True(offenders.Count == 0, "A control is disabled for a reason that is not in flight or data; a Role reason is removal (UX-DR27):\n" + string.Join('\n', offenders));
    }

    /// <summary>
    /// The disabled scan's planted signals: a Role-shaped binding on an unlisted file, a listed file's binding to a
    /// capability predicate, an <c>aria-disabled</c>, a disabled-looking class, and a permitted in-flight binding that must
    /// pass.
    /// </summary>
    [Fact]
    [Trait("Requirement", "UX-DR27")]
    public void The_disabled_scan_detects_each_planted_Role_binding()
    {
        Assert.NotEmpty(DisabledBindingsIn("BoardView.razor", """<button class="board__add" disabled="@(!_canManage)">"""));
        Assert.NotEmpty(DisabledBindingsIn("PrimaryButton.razor", """<button disabled="@(!TaskCapabilities.PermitsManageTasks(Role))">"""));
        Assert.NotEmpty(DisabledBindingsIn("TaskCard.razor", """<div aria-disabled="@IsViewer">"""));
        Assert.NotEmpty(DisabledBindingsIn("TaskCard.razor", """<div class="task-card task-card--disabled">"""));
        Assert.Empty(DisabledBindingsIn("PrimaryButton.razor", """<button class="button-primary" type="@ButtonType" disabled="@Disabled">"""));
        Assert.Matches(RenderTreeDisabled, """builder.AddAttribute(3, "disabled", !_canManage);""");
    }

    /// <summary>
    /// The client reaches no channel shared between tabs - no <c>BroadcastChannel</c>, no <c>SharedWorker</c>, no
    /// <c>storage</c> event - in its C#, its markup or the files it serves. A permission change reaches only the tab whose
    /// answer carried it; a broadcast would speak one Space's change in a tab showing another (UX-DR38).
    /// </summary>
    [Fact]
    [Trait("Requirement", "UX-DR38")]
    [Trait("Requirement", "UX-DR31")]
    public void No_client_code_shares_a_Role_or_a_narration_between_tabs()
    {
        var client = new DirectoryInfo(Path.Combine(RepositoryLayout.Root.FullName, "Yello.Client"));
        var sources = ClientSources(client, "*.cs").Concat(ClientSources(client, "*.razor")).Concat(ClientSources(client, "*.js")).Concat(ClientSources(client, "*.html")).ToList();

        Assert.Contains(sources, file => file.Name == "board-drag.js");

        var offenders = sources
            .Where(file => SharedChannel.IsMatch(WithoutComments(File.ReadAllText(file.FullName))))
            .Select(file => file.Name)
            .ToList();

        Assert.True(offenders.Count == 0, "Client code reaches a channel shared between tabs: " + string.Join(", ", offenders));
    }

    /// <summary>
    /// The shared-channel scan's planted signals, in the shapes an implementer would reach for.
    /// </summary>
    [Fact]
    [Trait("Requirement", "UX-DR38")]
    public void The_shared_channel_scan_detects_each_planted_channel()
    {
        Assert.Matches(SharedChannel, "const channel = new BroadcastChannel('yello-role');");
        Assert.Matches(SharedChannel, "new SharedWorker('/js/role.js')");
        Assert.Matches(SharedChannel, "window.addEventListener('storage', onRole);");
        Assert.Matches(SharedChannel, "window.onstorage = onRole;");
        Assert.DoesNotMatch(SharedChannel, WithoutComments("// nothing here reaches a BroadcastChannel"));
    }

    /// <summary>
    /// No file the client serves polls: no <c>setInterval</c> anywhere in its JavaScript, markup or C#. A permission change
    /// is pushed or read off an answer the tab asked for anyway, never polled (AD-9, AD-10). The .NET timers are held by
    /// <see cref="No_production_code_runs_on_a_timer_or_as_a_hosted_service"/>, which scans the client assembly too.
    /// </summary>
    [Fact]
    [Trait("Requirement", "AD-9")]
    [Trait("Requirement", "AD-10")]
    public void No_client_file_polls_on_an_interval()
    {
        var client = new DirectoryInfo(Path.Combine(RepositoryLayout.Root.FullName, "Yello.Client"));
        var sources = ClientSources(client, "*.js").Concat(ClientSources(client, "*.razor")).Concat(ClientSources(client, "*.cs")).Concat(ClientSources(client, "*.html")).ToList();

        Assert.Contains(sources, file => file.Name == "board-drag.js");

        var offenders = sources
            .Where(file => Interval.IsMatch(WithoutComments(File.ReadAllText(file.FullName))))
            .Select(file => file.Name)
            .ToList();

        Assert.True(offenders.Count == 0, "Client code polls on an interval: " + string.Join(", ", offenders));
        Assert.Matches(Interval, "this.#poll = setInterval(() => this.#reread(), 1000);");
        Assert.Matches(Interval, "window.setInterval(reread, 500)");
    }

    /// <summary>
    /// The Role narration's copy: one entry per Role a drop or a rise can land on, each "You're now a/an {Role}. {What it
    /// permits}." with the article in the value and no placeholder, so no Space name can ever be composed into it; the
    /// surface-left sentence keeps its one placeholder, the Role name; and the typed Space-settings URL has its own sentence.
    /// Every entry carries a comment.
    /// </summary>
    [Fact(Skip = "red: AC4, AC7 - the Role narration copy does not exist yet")]
    [Trait("Requirement", "UX-DR38")]
    [Trait("Requirement", "UX-DR29")]
    public void The_Role_narration_copy_names_the_Role_and_what_it_permits_and_no_Space()
    {
        var copy = XDocument.Load(Path.Combine(RepositoryLayout.Root.FullName, "Yello.Client", "Localisation", "ClientCopy.resx"))
            .Root!
            .Elements("data")
            .ToDictionary(data => (string)data.Attribute("name")!, data => ((string?)data.Element("value") ?? string.Empty, (string?)data.Element("comment") ?? string.Empty), StringComparer.Ordinal);

        var expected = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["RoleNarrationAdmin"] = "You're now an Admin. You can manage Members and settings.",
            ["RoleNarrationMember"] = "You're now a Member. You can create and edit Projects and Tasks.",
            ["RoleNarrationViewer"] = "You're now a Viewer. Read only in this Space.",
        };

        foreach (var (name, value) in expected)
        {
            Assert.True(copy.TryGetValue(name, out var entry), $"ClientCopy.resx has no {name}.");
            Assert.Equal(value, entry.Item1);
            Assert.DoesNotContain("{", entry.Item1, StringComparison.Ordinal);
            Assert.False(string.IsNullOrWhiteSpace(entry.Item2), $"{name} carries no comment.");
        }

        Assert.Equal("You're now a {0}. Space settings is no longer available.", copy["SpaceSettingsNoLongerAvailable"].Item1);
        Assert.Contains(copy.Values, entry => entry.Item1 == "Space settings is for Owners and Admins.");
    }

    /// <summary>
    /// Exactly one production type writes the Role header - the pipeline, which is the only reader of the judged Role - and
    /// no slice, endpoint or store names it. The client may read it; it is outside this scan.
    /// </summary>
    [Fact(Skip = RoleDropRed + "no production type writes the Role header yet")]
    [Trait("Requirement", "AD-1")]
    [Trait("Requirement", "AR-3")]
    public void Exactly_one_production_type_writes_the_Role_header()
    {
        var writers = new SortedSet<string>(StringComparer.Ordinal);

        foreach (var assembly in RuntimeAssemblies().Where(assembly => assembly != ProductionAssemblies.Client && assembly != ProductionAssemblies.Contracts))
        {
            using var module = ModuleDefinition.ReadModule(assembly.Location);

            foreach (var type in module.GetTypes().Where(NamesTheRoleHeader))
            {
                writers.Add(OutermostOf(type).FullName);
            }
        }

        Assert.Equal([PipelineTypeName], writers);
    }

    /// <summary>
    /// The cross-origin policy exposes the Role header, so a client served from another origin can read it on a
    /// credentialed answer. Without it the browser hides every non-safelisted response header from <c>fetch</c>, and the
    /// client would never learn a drop.
    /// </summary>
    [Fact(Skip = "red: AC4 - the cross-origin policy exposes no Role header yet")]
    [Trait("Requirement", "AD-9")]
    [Trait("Requirement", "UX-DR29")]
    public void The_cross_origin_policy_exposes_the_Role_header()
    {
        var options = new CorsOptions();

        CrossOriginPolicy.Configure(options, "https://client.yello.test");

        var policy = options.GetPolicy(CrossOriginPolicy.PolicyName);

        Assert.NotNull(policy);
        Assert.Contains(RoleHeaderName, policy.ExposedHeaders, StringComparer.OrdinalIgnoreCase);
    }

    private static HashSet<Capability> HeldBy(Role role) =>
        [.. Enum.GetValues<Capability>().Where(capability => RoleCapabilityMatrix.Decide(role, capability) != CapabilityDecision.Denied)];

    private static List<FileInfo> ClientSources(DirectoryInfo client, string pattern) =>
        [.. client.EnumerateFiles(pattern, SearchOption.AllDirectories)
            .Where(file => !file.FullName.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                && !file.FullName.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                && !file.FullName.Contains($"{Path.DirectorySeparatorChar}_framework{Path.DirectorySeparatorChar}", StringComparison.Ordinal))];

    /// <summary>
    /// Each <c>disabled</c> / <c>aria-disabled</c> binding in <paramref name="text"/> that is not a permitted one for
    /// <paramref name="file"/>, or names a capability predicate or a Role, and each disabled-looking class. Razor comments
    /// are removed first, because the files explain in prose why they are never disabled.
    /// </summary>
    private static IEnumerable<string> DisabledBindingsIn(string file, string text)
    {
        var markup = RazorComment.Replace(text, string.Empty);
        var permitted = PermittedDisabledBindings.GetValueOrDefault(file, []);

        foreach (Match binding in DisabledBinding.Matches(markup))
        {
            var expression = binding.Groups["expression"].Value;

            if (!permitted.Contains(expression, StringComparer.Ordinal) || RoleShaped.IsMatch(expression))
            {
                yield return $"{file}: {binding.Value}";
            }
        }

        foreach (Match looks in DisabledClass.Matches(markup))
        {
            yield return $"{file}: class {looks.Value}";
        }
    }

    private static string WithoutComments(string text) => LineOrBlockComment.Replace(RazorComment.Replace(text, string.Empty), string.Empty);

    private static bool NamesTheRoleHeader(TypeDefinition type) =>
        type.Methods.Any(method => method.HasBody && method.Body.Instructions.Any(instruction => NamesTheRoleHeader(instruction.Operand)));

    private static bool NamesTheRoleHeader(object? operand) => operand switch
    {
        string literal => string.Equals(literal, RoleHeaderName, StringComparison.OrdinalIgnoreCase),
        MethodReference called => called.DeclaringType.Name == "RoleHeader" && called.Name.StartsWith("get_", StringComparison.Ordinal),
        _ => false,
    };

    [GeneratedRegex(@"^\s*new MatrixRoute\(Capability\.(?<capability>\w+),", RegexOptions.Multiline, 1000)]
    private static partial Regex MatrixRouteRow { get; }

    [GeneratedRegex(@"(?<![\w-])(?:aria-)?disabled=""(?<expression>[^""]*)""", RegexOptions.IgnoreCase, 1000)]
    private static partial Regex DisabledBinding { get; }

    [GeneratedRegex(@"Permits|Capabilit|\bRole\b|\.Role\b|Viewer|_can[A-Z]", RegexOptions.None, 1000)]
    private static partial Regex RoleShaped { get; }

    [GeneratedRegex(@"[\w-]*(?:is-disabled|--disabled)\b", RegexOptions.None, 1000)]
    private static partial Regex DisabledClass { get; }

    [GeneratedRegex(@"AddAttribute\([^,]+,\s*""(?:aria-)?disabled""", RegexOptions.IgnoreCase, 1000)]
    private static partial Regex RenderTreeDisabled { get; }

    [GeneratedRegex(@"\bBroadcastChannel\b|\bSharedWorker\b|addEventListener\(\s*['""]storage['""]|\bonstorage\b", RegexOptions.None, 1000)]
    private static partial Regex SharedChannel { get; }

    [GeneratedRegex(@"\bsetInterval\s*\(", RegexOptions.None, 1000)]
    private static partial Regex Interval { get; }

    [GeneratedRegex(@"@\*.*?\*@|<!--.*?-->", RegexOptions.Singleline, 1000)]
    private static partial Regex RazorComment { get; }

    [GeneratedRegex(@"/\*.*?\*/|(?<![:'""])//[^\n]*", RegexOptions.Singleline, 1000)]
    private static partial Regex LineOrBlockComment { get; }
}
