using Mono.Cecil;
using Mono.Cecil.Cil;

namespace Yello.Tests.Architecture;

/// <summary>
/// Scans the compiled IL of every assembly in the solution for banned Role-API surface.
/// </summary>
/// <remarks>
/// <para>
/// Mono.Cecil rather than ArchUnitNET's fluent rules, because most of these bans need a
/// precision the type-level API cannot express. The ban on
/// <c>ClaimsPrincipal.IsInRole</c> is a ban on one <i>method</i>: <c>ClaimsPrincipal</c>
/// itself is entirely legitimate here, since Identity stays wired for authentication. A rule
/// phrased as "must not depend on ClaimsPrincipal" would forbid the thing that is allowed
/// and still not name the thing that is not.
/// </para>
/// <para>
/// <b>Scope is the whole solution, not just production code.</b> AC3 says "anywhere in the
/// solution" and AD-1 agrees. A fixture that seeds "an admin" through
/// <c>RoleManager&lt;&gt;</c> is the most likely first appearance of the shape this bans, and
/// from there it is the pattern production code copies. See <see cref="SolutionAssemblies"/>.
/// </para>
/// <para>
/// <b>What the ban covers, and why each form is here.</b> The four A-3 assertions are
/// grouped by what a reader would call them, not by IL mechanism - several of the forms below
/// are the <i>idiomatic</i> way to write role authorisation in ASP.NET Core, and a ban that
/// caught only the attribute would leave role-based authorisation fully wireable with every
/// assertion green:
/// </para>
/// <list type="bullet">
/// <item><c>[Authorize(Roles = ...)]</c> as a compile-time attribute, on any type that
/// derives from <c>AuthorizeAttribute</c> - not only the exact type.</item>
/// <item><c>new AuthorizeAttribute { Roles = "..." }</c>, which never appears in
/// <c>CustomAttributes</c> at all: it is an object initialiser, so it compiles to a
/// <c>set_Roles</c> call. This is the form Minimal APIs reach for
/// (<c>RequireAuthorization(new AuthorizeAttribute { Roles = "Admin" })</c>), and
/// <c>Yello.Host</c> is a Minimal API host.</item>
/// <item><c>AuthorizationPolicyBuilder.RequireRole(...)</c>, the standard policy-based role
/// check - the very thing a developer told "use a policy, not the Roles argument" would
/// write next.</item>
/// <item><c>IdentityBuilder.AddRoles&lt;TRole&gt;()</c> and <c>AddRoleManager&lt;T&gt;()</c>,
/// which is how the role store gets wired in the first place.</item>
/// </list>
/// <para>
/// <b>Known limits, stated rather than implied.</b> Reflective invocation
/// (<c>GetMethod("IsInRole").Invoke(...)</c>) is invisible to an IL scan, as is a role check
/// inside a third-party assembly this solution merely calls. The ban is on the shapes this
/// codebase can write, which is what AC3 governs.
/// </para>
/// </remarks>
internal static class RoleApiScan
{
    private const string AuthorizeAttributeFullName = "Microsoft.AspNetCore.Authorization.AuthorizeAttribute";
    private const string IdentityNamespaceRoot = "Microsoft.AspNetCore.Identity";
    private const string AuthorizationNamespaceRoot = "Microsoft.AspNetCore.Authorization";
    private const string IdentityBuilderFullName = "Microsoft.AspNetCore.Identity.IdentityBuilder";
    private const string PolicyBuilderFullName = "Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder";

    /// <summary>
    /// The principal types whose <c>IsInRole</c> is the banned one. Checking the declaring
    /// type matters in both directions: without it a same-named helper of your own
    /// (<c>SpacePolicy.IsInRole</c>) is a false positive, and the failure message would name
    /// a method that breaks no rule.
    /// </summary>
    private static readonly string[] PrincipalTypes =
    [
        "System.Security.Claims.ClaimsPrincipal",
        "System.Security.Principal.IPrincipal",
        "System.Security.Principal.GenericPrincipal",
        "System.Security.Principal.WindowsPrincipal",
    ];

    private static readonly Lazy<ScanResult> Result = new(Scan, isThreadSafe: true);

    /// <summary>
    /// Sites applying or constructing an <c>[Authorize]</c> carrying roles, and sites building
    /// a role requirement into a policy.
    /// </summary>
    public static IReadOnlyList<string> AuthorizeRolesUsages => Result.Value.AuthorizeRoles;

    /// <summary>
    /// Call sites invoking <c>IsInRole</c> on a principal.
    /// </summary>
    public static IReadOnlyList<string> IsInRoleCalls => Result.Value.IsInRoleCallSites;

    /// <summary>
    /// References to Identity's role entity family, in any arity.
    /// </summary>
    public static IReadOnlyList<string> IdentityRoleReferences => Result.Value.IdentityRoleTypes;

    /// <summary>
    /// References to Identity's role store or role manager, and the calls that wire them.
    /// </summary>
    public static IReadOnlyList<string> RoleStoreReferences => Result.Value.RoleStoreTypes;

    /// <summary>
    /// The four accumulators one pass over the bytecode fills.
    /// </summary>
    /// <remarks>
    /// The component names deliberately differ from the four public properties above. Naming
    /// them identically shadowed the outer members (S3218), and in a file where the outer
    /// property and the inner accumulator are one line apart that is a genuine reading hazard,
    /// not just a rule.
    /// </remarks>
    private sealed record ScanResult(
        List<string> AuthorizeRoles,
        List<string> IsInRoleCallSites,
        List<string> IdentityRoleTypes,
        List<string> RoleStoreTypes);

    /// <summary>
    /// A <c>set_Roles</c> call site, held until the set of authorize-attribute types is known.
    /// </summary>
    private sealed record PendingRolesSetter(string Location, string DeclaringTypeFullName);

    /// <summary>
    /// One pass over every assembly in the solution, then a resolution pass.
    /// </summary>
    /// <remarks>
    /// Split across a method per IL nesting level - module, type, method - rather than written
    /// as one nest of loops. Cecil's object graph is four levels deep before the first
    /// assertion is reachable, and a single method walking all four scored 31 against Sonar's
    /// cognitive-complexity budget of 15 (S3776) and breached the nesting limit twice (S134).
    /// The shape below is the same walk with each level named.
    /// </remarks>
    private static ScanResult Scan()
    {
        var result = new ScanResult([], [], [], []);

        // The base-type chain of every type this solution compiles, so a subclass of
        // AuthorizeAttribute is recognised as one. Framework types are not resolved - they do
        // not need to be, because a subclass written here is a TypeDefinition here.
        var baseTypes = new Dictionary<string, string>(StringComparer.Ordinal);
        var pendingSetters = new List<PendingRolesSetter>();

        foreach (var assemblyPath in SolutionAssemblies.AllFiles)
        {
            using var module = ReadModule(assemblyPath);
            ScanModule(module, result, baseTypes, pendingSetters);
        }

        ResolveRolesSetters(pendingSetters, baseTypes, result);

        return result;
    }

    private static ModuleDefinition ReadModule(string assemblyPath)
    {
        try
        {
            return ModuleDefinition.ReadModule(assemblyPath);
        }
        catch (Exception exception) when (exception is BadImageFormatException or IOException)
        {
            throw new InvalidOperationException(
                $"Gate C could not read '{assemblyPath}' as a managed assembly: " +
                $"{exception.Message}. The Role-API ban covers the whole solution, so an " +
                "assembly it cannot read is a gate that cannot answer, not one that passes.",
                exception);
        }
    }

    private static void ScanModule(
        ModuleDefinition module,
        ScanResult result,
        Dictionary<string, string> baseTypes,
        List<PendingRolesSetter> pendingSetters)
    {
        var assemblyName = module.Assembly.Name.Name;

        CollectRoleTypeReferences(module, assemblyName, result);

        foreach (var type in module.GetTypes())
        {
            if (type.BaseType is not null)
            {
                baseTypes[type.FullName] = type.BaseType.FullName;
            }

            ScanType(type, assemblyName, result, pendingSetters);
        }
    }

    /// <summary>
    /// The type-level bans, read from the module's type-reference table rather than from its
    /// members: a reference is a reference whether or not any code path reaches it.
    /// </summary>
    private static void CollectRoleTypeReferences(
        ModuleDefinition module,
        string assemblyName,
        ScanResult result)
    {
        foreach (var typeReference in module.GetTypeReferences())
        {
            ClassifyRoleType(typeReference, assemblyName, result);
        }
    }

    /// <summary>
    /// Any type under the Identity or Authorization namespaces whose own name mentions Role is
    /// role-API surface.
    /// </summary>
    /// <remarks>
    /// Deliberately a name test rather than an enumerated list. The list this replaced named
    /// <c>IdentityRole</c>, <c>RoleManager</c>, <c>IRoleStore</c> and <c>IRoleValidator</c>,
    /// and therefore missed <c>IdentityUserRole&lt;TKey&gt;</c> - the account-to-role join
    /// entity, i.e. the exact table this architecture rejects - along with
    /// <c>IRoleClaimStore&lt;TRole&gt;</c> and <c>IdentityRoleClaim&lt;TKey&gt;</c>. Every
    /// Identity type carrying "Role" in its name is role surface; there is no counter-example
    /// to exclude, so the general rule is both shorter and complete.
    /// </remarks>
    private static void ClassifyRoleType(TypeReference typeReference, string assemblyName, ScanResult result)
    {
        var ns = typeReference.Namespace ?? string.Empty;
        var name = typeReference.Name;

        if (!name.Contains("Role", StringComparison.Ordinal))
        {
            return;
        }

        var underIdentity = ns.StartsWith(IdentityNamespaceRoot, StringComparison.Ordinal);
        var underAuthorization = ns.StartsWith(AuthorizationNamespaceRoot, StringComparison.Ordinal);

        if (!underIdentity && !underAuthorization)
        {
            return;
        }

        // RolesAuthorizationRequirement and friends are the machinery behind
        // [Authorize(Roles = ...)], so they belong with A-3.1 rather than with the store.
        if (underAuthorization)
        {
            result.AuthorizeRoles.Add(
                $"{assemblyName}: references {typeReference.FullName}, which is role-based authorisation surface");
            return;
        }

        var isRoleEntity = name.StartsWith("IdentityRole", StringComparison.Ordinal)
            || name.StartsWith("IdentityUserRole", StringComparison.Ordinal);

        var bucket = isRoleEntity ? result.IdentityRoleTypes : result.RoleStoreTypes;
        bucket.Add($"{assemblyName}: references {typeReference.FullName}");
    }

    private static void ScanType(
        TypeDefinition type,
        string assemblyName,
        ScanResult result,
        List<PendingRolesSetter> pendingSetters)
    {
        CollectAuthorizeRoles(type.CustomAttributes, $"{assemblyName}: type {type.FullName}", result);

        foreach (var property in type.Properties)
        {
            CollectAuthorizeRoles(
                property.CustomAttributes,
                $"{assemblyName}: property {type.FullName}.{property.Name}",
                result);
        }

        foreach (var method in type.Methods)
        {
            ScanMethod(method, assemblyName, result, pendingSetters);
        }
    }

    private static void ScanMethod(
        MethodDefinition method,
        string assemblyName,
        ScanResult result,
        List<PendingRolesSetter> pendingSetters)
    {
        var location = $"{assemblyName}: {method.DeclaringType.FullName}.{method.Name}";

        CollectAuthorizeRoles(method.CustomAttributes, $"{assemblyName}: method {location}", result);

        if (!method.HasBody)
        {
            return;
        }

        foreach (var instruction in method.Body.Instructions)
        {
            ClassifyCall(instruction, location, result, pendingSetters);
        }
    }

    private static void ClassifyCall(
        Instruction instruction,
        string location,
        ScanResult result,
        List<PendingRolesSetter> pendingSetters)
    {
        if (instruction.Operand is not MethodReference called)
        {
            return;
        }

        var declaring = called.DeclaringType.FullName;
        var name = called.Name;

        if (name.Equals("IsInRole", StringComparison.Ordinal)
            && PrincipalTypes.Contains(declaring, StringComparer.Ordinal))
        {
            result.IsInRoleCallSites.Add($"{location} calls {declaring}.IsInRole");
        }
        else if (name.Equals("RequireRole", StringComparison.Ordinal)
            && declaring.Equals(PolicyBuilderFullName, StringComparison.Ordinal))
        {
            result.AuthorizeRoles.Add($"{location} calls AuthorizationPolicyBuilder.RequireRole");
        }
        else if ((name.Equals("AddRoles", StringComparison.Ordinal) || name.Equals("AddRoleManager", StringComparison.Ordinal))
            && declaring.Equals(IdentityBuilderFullName, StringComparison.Ordinal))
        {
            result.RoleStoreTypes.Add($"{location} calls IdentityBuilder.{name}");
        }
        else if (name.Equals("set_Roles", StringComparison.Ordinal))
        {
            // Held: whether this is banned depends on the declaring type deriving from
            // AuthorizeAttribute, and the base-type map is not complete until every module
            // has been read.
            pendingSetters.Add(new PendingRolesSetter(location, declaring));
        }
        else
        {
            // Every other call in the solution. The bans above are deliberately specific to a
            // method on a named declaring type: a broader match would report a same-named
            // helper of our own - SpacePolicy.IsInRole, say - and a gate whose failures a
            // developer learns to dismiss is worse than no gate.
        }
    }

    /// <summary>
    /// Decides the held <c>set_Roles</c> call sites now that every type's base is known.
    /// </summary>
    private static void ResolveRolesSetters(
        List<PendingRolesSetter> pendingSetters,
        Dictionary<string, string> baseTypes,
        ScanResult result)
    {
        foreach (var setter in pendingSetters.Where(s => IsAuthorizeAttribute(s.DeclaringTypeFullName, baseTypes)))
        {
            result.AuthorizeRoles.Add(
                $"{setter.Location} sets {setter.DeclaringTypeFullName}.Roles - an object " +
                "initialiser, which never appears as a compile-time attribute");
        }
    }

    private static void CollectAuthorizeRoles(
        IEnumerable<CustomAttribute> attributes,
        string location,
        ScanResult result)
    {
        foreach (var attribute in attributes)
        {
            CollectAuthorizeRoles(attribute, location, result);
        }
    }

    private static void CollectAuthorizeRoles(CustomAttribute attribute, string location, ScanResult result)
    {
        if (!IsAuthorizeAttributeType(attribute.AttributeType))
        {
            return;
        }

        // Roles is a settable property, so it arrives as a named argument rather than a
        // constructor argument - AuthorizeAttribute's only string constructor takes a
        // policy, which is the permitted shape.
        if (attribute.HasProperties
            && attribute.Properties.Any(property => property.Name.Equals("Roles", StringComparison.Ordinal)))
        {
            result.AuthorizeRoles.Add($"{location} is annotated [{attribute.AttributeType.Name}(Roles = ...)]");
        }
    }

    /// <summary>
    /// True for <c>AuthorizeAttribute</c> and for anything deriving from it.
    /// </summary>
    /// <remarks>
    /// Exact <c>FullName</c> equality was the previous test, and
    /// <c>class SpaceAuthorizeAttribute : AuthorizeAttribute</c> escaped it - a subclass being
    /// the natural way a codebase with a domain concept called Space would wrap the attribute
    /// in the first place.
    /// </remarks>
    private static bool IsAuthorizeAttributeType(TypeReference attributeType)
    {
        if (attributeType.FullName.Equals(AuthorizeAttributeFullName, StringComparison.Ordinal))
        {
            return true;
        }

        var definition = TryResolve(attributeType);

        while (definition?.BaseType is not null)
        {
            if (definition.BaseType.FullName.Equals(AuthorizeAttributeFullName, StringComparison.Ordinal))
            {
                return true;
            }

            definition = TryResolve(definition.BaseType);
        }

        return false;
    }

    /// <summary>
    /// Walks the collected base-type map, which covers every type this solution compiles.
    /// </summary>
    private static bool IsAuthorizeAttribute(string typeFullName, Dictionary<string, string> baseTypes)
    {
        var current = typeFullName;

        // The map is finite and the guard bounds the walk, so a cyclic map (which the CLR
        // forbids, but this map is built from untrusted metadata) cannot spin here.
        for (var depth = 0; depth < 32; depth++)
        {
            if (current.Equals(AuthorizeAttributeFullName, StringComparison.Ordinal))
            {
                return true;
            }

            if (!baseTypes.TryGetValue(current, out var parent))
            {
                return false;
            }

            current = parent;
        }

        return false;
    }

    private static TypeDefinition? TryResolve(TypeReference typeReference)
    {
        try
        {
            return typeReference.Resolve();
        }
        catch (AssemblyResolutionException)
        {
            // A framework or third-party assembly Cecil cannot find. Its own hierarchy is not
            // this solution's to police, and the collected base-type map covers every type
            // that is.
            return null;
        }
    }
}
