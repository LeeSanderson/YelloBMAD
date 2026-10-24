using Mono.Cecil;
using Xunit;

namespace Yello.Tests.Architecture;

/// <summary>
/// Story 1.9's gates owed by earlier stories (Task 10): the Account-scoped reads reachable only from
/// the Account-scoped surfaces, and the last-Space scan widened to the names it missed.
/// <c>deferred-work.md:344</c>.
/// </summary>
public sealed partial class SpaceSwitcherGateTests
{
    private static readonly string[] AccountScopedReads =
    [
        "Yello.Application.Spaces.IAccountSpaceDirectory",
        "Yello.Application.ApiTokens.IAccountApiTokenDirectory",
    ];

    /// <summary>
    /// The only types allowed to depend on an Account-scoped read: the handlers behind AD-24's two
    /// Account-scoped surfaces, the switcher and Account settings.
    /// </summary>
    private static readonly string[] AccountScopedReaders =
    [
        "Yello.Application.ApiTokens.ListApiTokens.ListApiTokensHandler",
        "Yello.Application.Spaces.ListSpaces.ListSpacesHandler",
    ];

    /// <summary>
    /// The endpoint methods behind AD-24's two Account-scoped surfaces: the switcher and Account
    /// settings' Token list. By method, because <c>ApiTokenEndpoints</c> also maps the Space-scoped
    /// issue route.
    /// </summary>
    private static readonly string[] AccountScopedEndpoints =
    [
        "Yello.Host.Endpoints.ApiTokenEndpoints::ListAsync",
        "Yello.Host.Endpoints.SpaceDirectoryEndpoints::ListAsync",
    ];

    /// <summary>
    /// 1.9-ARCH-005 / AD-2, AD-24 - a Space-scoped endpoint cannot reach the reads that see an
    /// Account's Memberships in every Space: the only production types that take
    /// <c>IAccountSpaceDirectory</c> or <c>IAccountApiTokenDirectory</c> as a parameter or a field, or call
    /// a method declared on either, are the two Account-scoped handlers; and the only methods that take
    /// or call those handlers are the two Account-scoped endpoints - each by exact list.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>No_caller_sets_both_session_context_keys</c> holds this per type; a Space-scoped endpoint
    /// taking the directory as a parameter would set one key in each of two units of work and pass it.
    /// The implementer and the interfaces themselves are not dependants. Read from the compiled IL,
    /// attributed to the outermost type, as <see cref="CallersOf"/> does.
    /// </para>
    /// <para>
    /// <b>Active - green today, a standing gate.</b> Planted on 2026-10-24 by giving
    /// <c>ActiveSpaceEndpoints</c> an <c>IAccountSpaceDirectory</c> parameter, and caught.
    /// </para>
    /// </remarks>
    [Fact]
    [Trait("Requirement", "AR-6")]
    [Trait("Requirement", "AD-2")]
    public void Only_the_Account_scoped_handlers_depend_on_the_Account_scoped_reads()
    {
        var dependants = DependantsOf(AccountScopedReads);

        // Vacuity guard: each Account-scoped read is found depended on, by its own handler.
        Assert.Contains(AccountScopedReaders[0], dependants);
        Assert.Contains(AccountScopedReaders[1], dependants);

        Assert.Equal(AccountScopedReaders, dependants);

        // The second hop: a Space-scoped endpoint that took a handler instead of the directory would
        // reach the same read. Only the two Account-scoped endpoint methods take or call the handlers.
        var handlerUsers = MethodsDependingOn(AccountScopedReaders);

        Assert.Contains(AccountScopedEndpoints[0], handlerUsers);
        Assert.Contains(AccountScopedEndpoints[1], handlerUsers);

        Assert.Equal(AccountScopedEndpoints, handlerUsers);
    }

    /// <summary>
    /// Every production method, as "outermost type::method" (a lambda is attributed to the method that
    /// declares it, by its compiler-generated name), that takes one of <paramref name="types"/> as a
    /// parameter or calls or constructs a member declared on one. Dependency-injection registration,
    /// which names the type only as a generic argument, is not a use.
    /// </summary>
    private static List<string> MethodsDependingOn(IReadOnlyCollection<string> types)
    {
        var users = new SortedSet<string>(StringComparer.Ordinal);

        foreach (var assembly in ProductionAssemblies.All.Where(assembly => assembly != ProductionAssemblies.AppHost))
        {
            using var module = ModuleDefinition.ReadModule(assembly.Location);

            var found = module.GetTypes()
                .Where(type => !types.Contains(type.FullName))
                .SelectMany(type => type.Methods
                    .Where(method =>
                        method.Parameters.Any(parameter => types.Contains(parameter.ParameterType.FullName))
                        || (method.HasBody && method.Body.Instructions.Any(instruction =>
                            instruction.Operand is MethodReference called && types.Contains(called.DeclaringType.FullName))))
                    .Select(method => $"{OutermostOf(type).FullName}::{SourceNameOf(type, method)}"));

            users.UnionWith(found);
        }

        return [.. users];
    }

    /// <summary>
    /// The source method a compiled method belongs to: an async or iterator body's <c>MoveNext</c>
    /// lives on a <c>&lt;ListAsync&gt;d__5</c> type, and a lambda is a <c>&lt;ListAsync&gt;b__0</c>
    /// method, so both read as <c>ListAsync</c>.
    /// </summary>
    private static string SourceNameOf(TypeDefinition type, MethodDefinition method)
    {
        var name = method.Name;

        if (!name.StartsWith('<') && type.Name.StartsWith('<'))
        {
            name = type.Name;
        }

        var close = name.IndexOf('>', StringComparison.Ordinal);

        return name.StartsWith('<') && close > 1 ? name[1..close] : name;
    }

    /// <summary>
    /// 1.9-ARCH-006 / UX-DR32 - the last-Space scan catches both word orders and the names it missed
    /// (<c>prior</c>, <c>history</c>, <c>lastUsed</c>, <c>mru</c>), and still leaves the client's own
    /// <c>SpaceDirectory</c> and <c>ActiveSpace</c> alone.
    /// </summary>
    [Fact]
    [Trait("Requirement", "UX-DR32")]
    public void The_last_Space_scan_detects_both_word_orders_and_the_names_it_missed()
    {
        Assert.Matches(RemembersASpace, "private Guid? _priorSpace;");
        Assert.Matches(RemembersASpace, "var spaceHistory = new List<Guid>();");
        Assert.Matches(RemembersASpace, "private Guid _lastUsedSpaceId;");
        Assert.Matches(RemembersASpace, "var mruSpaces = Load();");
        Assert.Matches(RemembersASpace, "private Guid? _spacePrevious;");

        Assert.DoesNotMatch(RemembersASpace, "var spaceLastModified = row.UpdatedAt;");
        Assert.DoesNotMatch(RemembersASpace, "@inject SpaceDirectory Directory");
        Assert.DoesNotMatch(RemembersASpace, "ActiveSpace.Current");
        Assert.DoesNotMatch(RemembersASpace, "// never a remembered last Space");
    }

    /// <summary>
    /// Every production type, by outermost name, that takes one of <paramref name="interfaces"/> as a
    /// method or constructor parameter or a field, or calls a method declared on one.
    /// </summary>
    private static List<string> DependantsOf(IReadOnlyCollection<string> interfaces)
    {
        var dependants = new SortedSet<string>(StringComparer.Ordinal);

        foreach (var assembly in ProductionAssemblies.All.Where(assembly => assembly != ProductionAssemblies.AppHost))
        {
            using var module = ModuleDefinition.ReadModule(assembly.Location);

            foreach (var type in module.GetTypes())
            {
                var implements = type.Interfaces.Any(implemented => interfaces.Contains(implemented.InterfaceType.FullName));
                var isOne = interfaces.Contains(type.FullName);

                var depends =
                    type.Fields.Any(field => interfaces.Contains(field.FieldType.FullName))
                    || type.Methods.Any(method =>
                        method.Parameters.Any(parameter => interfaces.Contains(parameter.ParameterType.FullName))
                        || (method.HasBody && method.Body.Instructions.Any(instruction =>
                            instruction.Operand is MethodReference called && interfaces.Contains(called.DeclaringType.FullName))));

                if (depends && !implements && !isOne)
                {
                    dependants.Add(OutermostOf(type).FullName);
                }
            }
        }

        return [.. dependants];
    }
}
