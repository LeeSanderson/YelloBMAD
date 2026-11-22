using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Routing;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Xunit;
using Yello.Domain;
using Yello.Domain.Accounts;
using Yello.Domain.Memberships;
using Yello.Domain.Tasks;
using Yello.Host.Spaces;

namespace Yello.Tests.Architecture;

/// <summary>
/// The structural gates of a Task's description: the change route requires the collaborative-edit capability and the read
/// route reading, both on both surfaces; the change contracts carry changes and versions and never text; the Task edit and
/// the Task answer gain no description; one projector writes the projection; the client's predicate agrees with the matrix;
/// the editor is never disabled, read-only or inert; and attribution reads a live Account name only in named methods.
/// </summary>
/// <remarks>
/// <para>
/// <b>The subjects are found by name</b>, so a missing member fails here with the name it lost rather than as a compile
/// error elsewhere. The names are the story's recommendation (routes, contract types, slices, the store); a different name
/// chosen in the develop phase corrects the constant here and is recorded.
/// </para>
/// <para>
/// <b>Every scan has an oracle</b> over a planted type, so an empty result is never a scan that saw nothing.
/// </para>
/// </remarks>
public sealed partial class RefusalBoundaryGateTests
{
    private const string DescriptionChangesPattern = "/api/v1/spaces/{spaceId:guid}/tasks/{taskId:guid}/description/changes";

    private const string DescriptionPattern = "/api/v1/spaces/{spaceId:guid}/tasks/{taskId:guid}/description";

    private const string DescriptionStoreName = "Yello.Infrastructure.Persistence.TaskDescriptionStore";

    private const string EditsADescription = "Yello.Application.Tasks.EditTaskDescription.EditTaskDescriptionHandler";

    private const string ReadsADescription = "Yello.Application.Tasks.ViewTaskDescription.ViewTaskDescriptionHandler";

    private const string DescriptionProperty = "Description";

    /// <summary>
    /// Every method that reads <c>Account.DisplayName</c> today, as "outermost type.member", before the description store
    /// joins them. Tightened from whole types, so a second reader method in an admitted type is caught.
    /// </summary>
    private static readonly string[] DisplayNameReaderMethods =
    [
        "Yello.Infrastructure.Persistence.Configurations.AccountConfiguration.Configure",
        "Yello.Infrastructure.Persistence.InvitationStore.IssueAsync",
        "Yello.Infrastructure.Persistence.MembershipStore.AssignmentNoticeAsync",
        "Yello.Infrastructure.Persistence.MembershipStore.DisplayNameOfAsync",
        "Yello.Infrastructure.Persistence.MembershipStore.ListAssigneesAsync",
        "Yello.Infrastructure.Persistence.MembershipStore.ListAsync",
        "Yello.Infrastructure.Persistence.MembershipStore.OwnershipOfferNoticeAsync",
        "Yello.Infrastructure.Persistence.MembershipStore.ReadAsync",
        "Yello.Infrastructure.Persistence.MembershipStore.RowAsync",
    ];

    /// <summary>
    /// <c>POST</c> on the change route requires <c>EditTaskDescriptionCollaboratively</c>, serves both surfaces, consults no
    /// bound, and carries exactly the Space and the Task in its path.
    /// </summary>
    [Fact]
    [Trait("Requirement", "FR-16")]
    [Trait("Requirement", "FR-31")]
    [Trait("Requirement", "FR-35")]
    [Trait("Requirement", "AD-11")]
    public void The_description_change_route_requires_the_collaborative_edit_capability_on_both_surfaces()
    {
        var route = Assert.Single(RouteTable(), endpoint => endpoint.RoutePattern.RawText == DescriptionChangesPattern);

        Assert.Equal(["POST"], route.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods ?? []);
        Assert.Equal(Capability.EditTaskDescriptionCollaboratively, route.Metadata.GetMetadata<RequiredCapability>()?.Capability);
        Assert.DoesNotContain(route.Metadata, metadata => metadata.GetType().FullName == SessionOnlyMarkerName);
        Assert.Null(route.Metadata.GetMetadata<RequiredBound>());
        Assert.Equal(["spaceId", "taskId"], route.RoutePattern.Parameters.Select(parameter => parameter.Name).Order(StringComparer.Ordinal));
    }

    /// <summary>
    /// <c>GET</c> on the read route requires <c>ReadProjectsAndTasks</c>, so a Viewer reads the description, and serves both
    /// surfaces. No other method is mapped on it.
    /// </summary>
    [Fact]
    [Trait("Requirement", "FR-31")]
    [Trait("Requirement", "FR-35")]
    [Trait("Requirement", "AR-19")]
    public void The_description_read_route_requires_reading_on_both_surfaces()
    {
        var route = Assert.Single(RouteTable(), endpoint => endpoint.RoutePattern.RawText == DescriptionPattern);

        Assert.Equal(["GET"], route.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods ?? []);
        Assert.Equal(Capability.ReadProjectsAndTasks, route.Metadata.GetMetadata<RequiredCapability>()?.Capability);
        Assert.DoesNotContain(route.Metadata, metadata => metadata.GetType().FullName == SessionOnlyMarkerName);
    }

    /// <summary>
    /// <c>TaskRoutes</c> names both templates once, and each builder builds its path under the Task's own path.
    /// </summary>
    [Fact]
    [Trait("Requirement", "AR-35")]
    public void TaskRoutes_names_the_description_templates_once()
    {
        var routes = typeof(Yello.Contracts.Tasks.TaskRoutes);
        var (spaceId, taskId) = (Guid.NewGuid(), Guid.NewGuid());
        var taskPath = Yello.Contracts.Tasks.TaskRoutes.TaskOf(spaceId, taskId);

        foreach (var (template, builder, pattern, suffix) in new[]
        {
            ("Description", "DescriptionOf", DescriptionPattern, "/description"),
            ("DescriptionChanges", "DescriptionChangesOf", DescriptionChangesPattern, "/description/changes"),
        })
        {
            var property = routes.GetProperty(template, BindingFlags.Public | BindingFlags.Static);
            var pathOf = routes.GetMethod(builder, BindingFlags.Public | BindingFlags.Static, [typeof(Guid), typeof(Guid)]);

            Assert.True(property is not null, $"TaskRoutes has no static {template} property.");
            Assert.True(pathOf is not null, $"TaskRoutes has no static {builder}(Guid, Guid).");
            Assert.Equal(pattern, property.GetValue(null));
            Assert.Equal(taskPath + suffix, pathOf.Invoke(null, [spaceId, taskId]));
        }
    }

    /// <summary>
    /// The change request carries the batch of encoded changes and the client's encoded version, and nothing else: no text in
    /// any form, no Space. Bytes are <c>ReadOnlyMemory&lt;byte&gt;</c>, never a string.
    /// </summary>
    [Fact]
    [Trait("Requirement", "AD-11")]
    [Trait("Requirement", "AR-20")]
    [Trait("Requirement", "AD-3")]
    public void The_change_request_carries_changes_and_a_version_and_never_text()
    {
        var request = ContractNamed("TaskDescriptionChangesRequest");

        Assert.Equal(["Changes", "Version"], PropertyNames(request));
        Assert.Equal(typeof(ReadOnlyMemory<byte>), request.GetProperty("Version")?.PropertyType);
        Assert.DoesNotContain(request.GetProperties(), property => property.PropertyType == typeof(string));
    }

    /// <summary>
    /// The change answer lists the refusals, the server's version and the changes the client lacks, and never text for the
    /// client to adopt as truth.
    /// </summary>
    [Fact]
    [Trait("Requirement", "AD-11")]
    [Trait("Requirement", "AR-20")]
    public void The_change_answer_carries_refusals_a_version_and_the_missing_changes_and_never_text()
    {
        var response = ContractNamed("TaskDescriptionChangesResponse");

        Assert.Equal(["Missing", "Refused", "Version"], PropertyNames(response));
        Assert.Equal(typeof(ReadOnlyMemory<byte>), response.GetProperty("Version")?.PropertyType);
        Assert.DoesNotContain(response.GetProperties(), property => property.PropertyType == typeof(string));
    }

    /// <summary>
    /// The read answer carries the projection as text, the encoded state an editor bootstraps from and the server's version,
    /// and no Space.
    /// </summary>
    [Fact]
    [Trait("Requirement", "AR-19")]
    [Trait("Requirement", "AD-3")]
    public void The_read_answer_carries_the_projection_a_state_and_a_version()
    {
        var response = ContractNamed("TaskDescriptionResponse");

        Assert.Equal(["State", "Text", "Version"], PropertyNames(response));
        Assert.Equal(typeof(string), response.GetProperty("Text")?.PropertyType);
        Assert.Equal(typeof(ReadOnlyMemory<byte>), response.GetProperty("State")?.PropertyType);
    }

    /// <summary>
    /// The write and the read are one slice each, sealed classes with a <c>HandleAsync</c>, beside the other Task slices.
    /// </summary>
    [Fact]
    [Trait("Requirement", "AR-3")]
    public void The_description_write_and_read_are_one_slice_each()
    {
        foreach (var name in new[] { EditsADescription, ReadsADescription })
        {
            var handler = ProductionAssemblies.Application.GetType(name);

            Assert.True(handler is not null, $"{name} does not exist.");
            Assert.True(handler.IsSealed, $"{name} is not sealed.");
            Assert.Contains(handler.GetMethods(BindingFlags.Public | BindingFlags.Instance), method => method.Name == "HandleAsync");
        }
    }

    /// <summary>
    /// A title edit cannot reach the projection: the Task edit (<c>TaskEdit</c>), its request and the Host's body reader name
    /// no description, and the Task answer every Board, List and Task view shares carries none. Green today, and it must stay
    /// so: a standing gate.
    /// </summary>
    [Fact]
    [Trait("Requirement", "AD-13")]
    [Trait("Requirement", "AR-19")]
    [Trait("Requirement", "NFR-5")]
    public void No_Task_edit_contract_and_no_Task_answer_names_a_description()
    {
        Type[] carriers =
        [
            typeof(TaskEdit),
            typeof(Yello.Contracts.Tasks.EditTaskRequest),
            typeof(Yello.Contracts.Tasks.TaskResponse),
        ];

        foreach (var type in carriers)
        {
            Assert.DoesNotContain(
                type.GetProperties(BindingFlags.Public | BindingFlags.Instance),
                property => property.Name.Contains(DescriptionProperty, StringComparison.OrdinalIgnoreCase));
        }

        var body = Assert.Single(ProductionAssemblies.Host.GetTypes(), type => type.FullName == "Yello.Host.Endpoints.TaskEditBody");
        using var module = ModuleDefinition.ReadModule(ProductionAssemblies.Host.Location);
        var literals = module.GetTypes()
            .Where(type => OutermostOf(type).FullName == body.FullName)
            .SelectMany(type => type.Methods.Where(method => method.HasBody))
            .SelectMany(method => method.Body.Instructions.Where(instruction => instruction.OpCode == OpCodes.Ldstr).Select(instruction => instruction.Operand).OfType<string>())
            .ToList();

        // Anchor: the reader names the title it parses, so the scan reads its literals.
        Assert.Contains(literals, literal => literal.Equals("title", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(literals, literal => literal.Contains("description", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// A-11: nothing but the projector writes a Task's description, whether through the setter, an initialiser or an
    /// <c>ExecuteUpdate</c> naming it: exactly one method writes it, in the description store.
    /// </summary>
    [Fact]
    [Trait("Requirement", "AR-19")]
    [Trait("Requirement", "AD-13")]
    [Trait("Priority", "P1")]
    public void Only_the_projector_writes_a_Tasks_description()
    {
        var writers = PropertyWriters(RuntimeAssemblies().Select(assembly => assembly.Location), typeof(TaskItem).FullName!, DescriptionProperty);

        var projector = Assert.Single(writers);

        Assert.StartsWith(DescriptionStoreName + ".", projector, StringComparison.Ordinal);
    }

    /// <summary>
    /// A-11's raw SQL half: no production statement sets a <c>Description</c> column. The projector writes it through EF.
    /// Green today, and a standing gate.
    /// </summary>
    [Fact]
    [Trait("Requirement", "AR-19")]
    [Trait("Requirement", "AD-13")]
    [Trait("Priority", "P1")]
    public void No_raw_SQL_in_production_sets_a_description()
    {
        Assert.Empty(ColumnSqlWriters(RuntimeAssemblies().Select(assembly => assembly.Location), DescriptionProperty));
    }

    /// <summary>
    /// The oracle for both A-11 scans: over <see cref="PlantedDescriptionWriter"/>, the setter scan reports its direct
    /// assignment, its initialiser and its <c>ExecuteUpdate</c>-shaped write and not its read, and the SQL scan reports the
    /// statement that sets the column and not the one that filters on it.
    /// </summary>
    [Fact]
    [Trait("Requirement", "AR-19")]
    [Trait("Priority", "P1")]
    public void The_description_writer_scans_report_a_planted_writer()
    {
        var planted = typeof(PlantedDescriptionWriter).FullName!;
        var here = typeof(PlantedDescriptionWriter).Assembly.Location;

        Assert.Equal(
            [$"{planted}.{nameof(PlantedDescriptionWriter.Assign)}", $"{planted}.{nameof(PlantedDescriptionWriter.Initialise)}", $"{planted}.{nameof(PlantedDescriptionWriter.Project)}"],
            PropertyWriters([here], typeof(PlantedDescribedTask).FullName!, DescriptionProperty).Where(name => name.StartsWith(planted, StringComparison.Ordinal)));
        Assert.Equal([planted], ColumnSqlWriters([here], DescriptionProperty).Where(name => name == planted));
        Assert.True(SetsColumn(PlantedDescriptionWriter.Sets(), DescriptionProperty), "The SQL scan misses a SET of Description.");
        Assert.False(SetsColumn(PlantedDescriptionWriter.Filters(), DescriptionProperty), "The SQL scan reads a Description predicate as a write.");
    }

    /// <summary>
    /// The client's predicate for offering the description editor agrees with the capability matrix for every Role: it offers
    /// it exactly where <c>EditTaskDescriptionCollaboratively</c> is not denied. Sibling of the Task controls predicate gate.
    /// </summary>
    [Fact]
    [Trait("Requirement", "UX-DR27")]
    [Trait("Requirement", "UX-DR10")]
    [Trait("Requirement", "AD-1")]
    public void The_clients_description_editor_predicate_agrees_with_the_capability_matrix()
    {
        var predicate = ProductionAssemblies.Client.GetTypes()
            .SelectMany(type => type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static))
            .SingleOrDefault(method => method.Name.Contains("EditDescription", StringComparison.Ordinal)
                && method.ReturnType == typeof(bool)
                && method.GetParameters() is [{ ParameterType: var only }]
                && only == typeof(string));

        Assert.True(predicate is not null, "No static bool (string roleCode) method named for EditDescription in Yello.Client.");

        foreach (var role in Enum.GetValues<Role>())
        {
            var expected = RoleCapabilityMatrix.Decide(role, Capability.EditTaskDescriptionCollaboratively) != CapabilityDecision.Denied;

            Assert.True(
                predicate.Invoke(null, [StableCode.Of(role)]) is bool shown && shown == expected,
                $"The client's description predicate disagrees with the matrix for {role}.");
        }
    }

    /// <summary>
    /// A Role that may not edit gets no editor at all, never a disabled, read-only or inert one: the component's markup
    /// carries none of those attributes.
    /// </summary>
    [Fact]
    [Trait("Requirement", "UX-DR27")]
    [Trait("Requirement", "UX-DR10")]
    public void The_description_editor_is_never_disabled_read_only_or_inert()
    {
        var file = new FileInfo(Path.Combine(RepositoryLayout.Root.FullName, "Yello.Client", "Components", "DescriptionEditor.razor"));

        Assert.True(file.Exists, "DescriptionEditor.razor does not exist in Yello.Client/Components.");

        var markup = File.ReadAllText(file.FullName);

        Assert.False(DisabledAttribute.IsMatch(markup), "DescriptionEditor.razor carries a disabled attribute; a missing capability is absent (UX-DR27).");
        Assert.False(ReadOnlyOrInert.IsMatch(markup), "DescriptionEditor.razor carries readonly or inert; the revoked editor is a later story's state.");
    }

    /// <summary>
    /// Attribution reads a live Account name only in named methods: today's readers, and exactly one method of the
    /// description store, the one that captures the author's name at write time.
    /// </summary>
    [Fact]
    [Trait("Requirement", "UX-DR34")]
    [Trait("Requirement", "FR-3")]
    public void Attribution_reads_a_live_Account_name_only_in_named_methods()
    {
        var readers = AccountReaderMembers(RuntimeAssemblies().Select(assembly => assembly.Location), "get_DisplayName");
        var store = readers.Where(reader => reader.StartsWith(DescriptionStoreName + ".", StringComparison.Ordinal)).ToList();

        Assert.Single(store);
        Assert.Equal(DisplayNameReaderMethods.Order(StringComparer.Ordinal), readers.Except(store).Order(StringComparer.Ordinal));
    }

    /// <summary>
    /// The oracle for the method-level reader scan: over <see cref="PlantedAccountNameReader"/>, it reports the method that
    /// reads the name, through its lambda, and not the method beside it that reads only the id.
    /// </summary>
    [Fact]
    [Trait("Requirement", "UX-DR34")]
    public void The_method_level_Account_name_scan_reports_a_planted_reader()
    {
        var planted = typeof(PlantedAccountNameReader).FullName!;
        var found = AccountReaderMembers([typeof(PlantedAccountNameReader).Assembly.Location], "get_DisplayName")
            .Where(name => name.StartsWith(planted + ".", StringComparison.Ordinal));

        Assert.Equal([$"{planted}.{nameof(PlantedAccountNameReader.Capture)}"], found);
    }

    private static Type ContractNamed(string name)
    {
        var type = ProductionAssemblies.Contracts.GetType(TaskContractsNamespace + "." + name);

        Assert.True(type is not null, $"Yello.Contracts.Tasks.{name} does not exist.");
        Assert.DoesNotContain(type.GetProperties(), property => property.Name.Equals("SpaceId", StringComparison.OrdinalIgnoreCase));

        return type;
    }

    /// <summary>
    /// Every method that writes <paramref name="property"/> on <paramref name="declaringType"/> - a call to its setter (an
    /// initialiser compiles to one), or a <c>SetProperty</c> call built over its getter - as "outermost type.member".
    /// </summary>
    private static SortedSet<string> PropertyWriters(IEnumerable<string> locations, string declaringType, string property)
    {
        var writers = new SortedSet<string>(StringComparer.Ordinal);

        foreach (var location in locations)
        {
            using var module = ModuleDefinition.ReadModule(location);

            foreach (var (type, method) in module.GetTypes().SelectMany(type => type.Methods.Where(method => WritesProperty(method, declaringType, property)).Select(method => (type, method))))
            {
                writers.Add($"{OutermostOf(type).FullName}.{SourceMemberOf(type, method)}");
            }
        }

        return writers;
    }

    /// <summary>
    /// Whether a method calls the property's setter, or builds a <c>SetProperty</c> call over its getter.
    /// </summary>
    private static bool WritesProperty(MethodDefinition method, string declaringType, string property)
    {
        if (!method.HasBody)
        {
            return false;
        }

        var calls = method.Body.Instructions.Select(instruction => instruction.Operand).OfType<MethodReference>().ToList();
        var setsDirectly = calls.Exists(called => called.Name == "set_" + property && called.DeclaringType.FullName == declaringType);

        return setsDirectly || (calls.Exists(called => called.Name == "SetProperty")
            && method.Body.Instructions.Any(instruction => instruction.OpCode == OpCodes.Ldtoken
                && instruction.Operand is MethodReference getter
                && getter.Name == "get_" + property
                && getter.DeclaringType.FullName == declaringType));
    }

    /// <summary>
    /// Every type whose IL holds a string literal that sets <paramref name="column"/> in a <c>SET</c> list, by outermost type.
    /// </summary>
    private static SortedSet<string> ColumnSqlWriters(IEnumerable<string> locations, string column)
    {
        var writers = new SortedSet<string>(StringComparer.Ordinal);

        foreach (var location in locations)
        {
            using var module = ModuleDefinition.ReadModule(location);

            foreach (var type in module.GetTypes())
            {
                var writes = type.Methods.Any(method => method.HasBody && method.Body.Instructions.Any(instruction =>
                    instruction.OpCode == OpCodes.Ldstr && instruction.Operand is string literal && SetsColumn(literal, column)));

                if (writes)
                {
                    writers.Add(OutermostOf(type).FullName);
                }
            }
        }

        return writers;
    }

    private static bool SetsColumn(string statement, string column) =>
        SetList.Matches(statement).Any(match => Regex.IsMatch(
            match.Groups["list"].Value,
            $@"(?:^|[\s,.\[]){Regex.Escape(column)}\]?\s*=",
            RegexOptions.None,
            TimeSpan.FromSeconds(5)));

    /// <summary>
    /// Every method that reads one of <paramref name="getters"/> on <c>Account</c>, as "outermost type.member", the member
    /// read through the compiler's async and lambda names.
    /// </summary>
    private static SortedSet<string> AccountReaderMembers(IEnumerable<string> locations, params string[] getters)
    {
        var readers = new SortedSet<string>(StringComparer.Ordinal);

        foreach (var location in locations)
        {
            using var module = ModuleDefinition.ReadModule(location);

            foreach (var type in module.GetTypes())
            {
                foreach (var method in type.Methods.Where(method => method.HasBody && method.Body.Instructions.Any(instruction =>
                    instruction.Operand is MethodReference called
                    && getters.Contains(called.Name, StringComparer.Ordinal)
                    && called.DeclaringType.FullName == AccountTypeName)))
                {
                    readers.Add($"{OutermostOf(type).FullName}.{SourceMemberOf(type, method)}");
                }
            }
        }

        return readers;
    }

    [GeneratedRegex(@"(?<![\w-])(readonly|inert)\b", RegexOptions.IgnoreCase, 1000)]
    private static partial Regex ReadOnlyOrInert { get; }
}

/// <summary>
/// An entity shaped like a Task for the A-11 scans' oracle: the scans are run over its <c>Description</c> because a planted
/// writer of the real one would be a production violation.
/// </summary>
internal sealed class PlantedDescribedTask
{
    public string Description { get; set; } = string.Empty;
}

/// <summary>
/// A planted writer of a description, for the A-11 scans' oracle: a direct assignment, an initialiser, an
/// <c>ExecuteUpdate</c>-shaped write and a read, and two statements, one that sets the column and one that filters on it.
/// </summary>
internal static class PlantedDescriptionWriter
{
    public static void Assign(PlantedDescribedTask task, string text) => task.Description = text;

    public static PlantedDescribedTask Initialise(string text) => new() { Description = text };

    public static void Project(IEnumerable<PlantedDescribedTask> tasks, string text) => SetProperty(task => task.Description, text, tasks);

    public static string Read(PlantedDescribedTask task) => task.Description;

    public static string Sets() => "UPDATE dbo.[Task] SET Description = {0} WHERE Id = {1}";

    public static string Filters() => "UPDATE dbo.[Task] SET Title = {0} WHERE Description = {1}";

    private static void SetProperty(System.Linq.Expressions.Expression<Func<PlantedDescribedTask, string>> property, string value, IEnumerable<PlantedDescribedTask> tasks)
    {
        if (property.Body is not System.Linq.Expressions.MemberExpression { Member: PropertyInfo setter })
        {
            return;
        }

        foreach (var task in tasks)
        {
            setter.SetValue(task, value);
        }
    }
}

/// <summary>
/// A planted reader of an Account's display name, for the method-level scan's oracle: one method captures the name through
/// a lambda, the one beside it reads only the id.
/// </summary>
internal static class PlantedAccountNameReader
{
    public static IEnumerable<string> Capture(IEnumerable<Account> accounts) => accounts.Select(account => account.DisplayName);

    public static Guid Identify(Account account) => account.Id;
}
