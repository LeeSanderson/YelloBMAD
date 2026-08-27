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
/// <item><c>UserManager&lt;&gt;.AddToRoleAsync</c>, <c>IsInRoleAsync</c>, <c>GetRolesAsync</c>
/// and their siblings. <c>UserManager&lt;&gt;</c> is a <i>permitted</i> type - it is the Account
/// store, which is Identity wired for authentication - so its role surface reaches Identity's
/// roles while naming nothing this ban previously matched.</item>
/// <item><c>ClaimTypes.Role</c>, and the URI it expands to. A policy built from
/// <c>RequireClaim(ClaimTypes.Role, "Admin")</c> is role-based authorisation that touches no
/// banned namespace, method or type - the claim type is banned rather than
/// <c>RequireClaim</c>/<c>HasClaim</c>/<c>FindFirst</c>, because it is the role that cannot
/// express Yello's authorisation, not the act of reading a claim. See
/// <see cref="ClassifyRoleClaim"/>.</item>
/// </list>
/// <para>
/// <b>Known limits, stated rather than implied.</b> Reflective invocation
/// (<c>GetMethod("IsInRole").Invoke(...)</c>) is invisible to an IL scan, as is a role check
/// inside a third-party assembly this solution merely calls. A role modelled under a name this
/// scan cannot recognise - a bespoke <c>"role"</c> string claim of your own, say - is also out
/// of reach, and deliberately so: at that point it is not Identity's role API, it is a design
/// decision for review rather than a build gate. The ban is on the shapes this codebase can
/// write against the framework, which is what AC3 governs.
/// </para>
/// <para>
/// The last two bullets above were added in the second review pass, and the reason is worth
/// keeping: the first pass widened this ban from four named shapes to the idiomatic ones, and
/// the plants validated exactly the shapes the finding had listed. Both routes below were
/// reachable with all four A-3 assertions green, which is the same defect one level along.
/// </para>
/// </remarks>
internal static class RoleApiScan
{
    private const string AuthorizeAttributeFullName = "Microsoft.AspNetCore.Authorization.AuthorizeAttribute";
    private const string IdentityNamespaceRoot = "Microsoft.AspNetCore.Identity";
    private const string AuthorizationNamespaceRoot = "Microsoft.AspNetCore.Authorization";
    private const string IdentityBuilderFullName = "Microsoft.AspNetCore.Identity.IdentityBuilder";
    private const string PolicyBuilderFullName = "Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder";
    private const string ClaimTypesFullName = "System.Security.Claims.ClaimTypes";

    /// <summary>
    /// <c>UserManager&lt;TUser&gt;</c> is a permitted type - it is the Account store, which is
    /// Identity wired for authentication - but its role surface is not. The generic arity is
    /// part of the emitted name, so this is matched as a prefix.
    /// </summary>
    private const string UserManagerFullNamePrefix = "Microsoft.AspNetCore.Identity.UserManager";

    /// <summary>
    /// The value of <c>ClaimTypes.Role</c>. Banned as a literal too, so the ban cannot be
    /// stepped around by writing out the URI the constant expands to.
    /// </summary>
    /// <remarks>
    /// Assembled rather than written, because this scan reads every assembly in the solution
    /// including its own: as a single <c>const</c> the URI is one <c>ldstr</c> in
    /// <c>Yello.Tests.Architecture</c> and the gate reported itself as a violation the first
    /// time it ran. The same trick, for the same reason, as
    /// <c>TestingConventionTests.SqlServerImageLiteral</c>. Splitting it means no single literal
    /// in IL equals the value being matched.
    /// </remarks>
    private static readonly string RoleClaimTypeUri = string.Concat(
        "http://schemas.microsoft.com/ws/2008/06/identity/claims", "/", "role");

    /// <summary>
    /// <c>UserManager&lt;&gt;</c> methods that read or write Identity roles.
    /// </summary>
    private static readonly string[] UserManagerRoleMethods =
    [
        "AddToRoleAsync",
        "AddToRolesAsync",
        "RemoveFromRoleAsync",
        "RemoveFromRolesAsync",
        "GetRolesAsync",
        "IsInRoleAsync",
        "GetUsersInRoleAsync",
    ];

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
            ClassifyRoleClaim(instruction, location, result);
        }
    }

    /// <summary>
    /// The role <i>claim</i> - the route to role-based authorisation that touches no banned
    /// namespace, method or type.
    /// </summary>
    /// <remarks>
    /// <c>RequireClaim(ClaimTypes.Role, "Admin")</c>, <c>principal.HasClaim(ClaimTypes.Role, …)</c>
    /// and <c>FindFirst(ClaimTypes.Role)</c> are invisible to <see cref="ClassifyCall"/>:
    /// <c>RequireClaim</c>, <c>HasClaim</c> and <c>FindFirst</c> are general-purpose methods
    /// that cannot be banned wholesale, and <c>ClaimTypes.Role</c> is a field load - so
    /// <c>ClassifyCall</c> returns at its <c>is not MethodReference</c> guard before seeing it.
    /// <c>System.Security.Claims.ClaimTypes</c> is neither under a banned namespace nor named
    /// <c>*Role*</c>, so the type scan misses it too.
    /// <para>
    /// Banning the claim type rather than the methods is what makes this precise: it is the
    /// <i>role</i> that cannot express Yello's authorisation, not the act of reading a claim.
    /// Nothing legitimate in this codebase needs it - a Role here is an attribute of a
    /// Membership, so the same Account holds different Roles in different Spaces and a single
    /// claim on the principal cannot say which. This is the first thing a developer told "use a
    /// policy, not RequireRole" writes next, which is the same reasoning that put
    /// <c>RequireRole</c> in the ban.
    /// </para>
    /// </remarks>
    private static void ClassifyRoleClaim(Instruction instruction, string location, ScanResult result)
    {
        if (instruction.Operand is FieldReference field
            && field.Name.Equals("Role", StringComparison.Ordinal)
            && field.DeclaringType.FullName.Equals(ClaimTypesFullName, StringComparison.Ordinal))
        {
            result.AuthorizeRoles.Add($"{location} reads ClaimTypes.Role");
        }
        else if (instruction.Operand is string text
            && text.Equals(RoleClaimTypeUri, StringComparison.OrdinalIgnoreCase))
        {
            result.AuthorizeRoles.Add(
                $"{location} states the role claim type as a literal URI, which is " +
                "ClaimTypes.Role spelled out");
        }
        else
        {
            // Every other field load and string literal in the solution. Only the role claim
            // type is banned here; ClaimTypes itself and every other claim are permitted,
            // because Identity stays wired for authentication and claims are how it carries
            // identity. The ban is on the one claim that would express authorisation.
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
        else if (UserManagerRoleMethods.Contains(name, StringComparer.Ordinal)
            && declaring.StartsWith(UserManagerFullNamePrefix, StringComparison.Ordinal))
        {
            // UserManager<> reaches Identity's roles without naming RoleManager, IRoleStore or
            // IdentityRole anywhere - so all four A-3 assertions passed while roles were being
            // assigned and read through a type the ban permits, because Identity-for-
            // authentication is exactly what UserManager<> is for.
            result.RoleStoreTypes.Add($"{location} calls UserManager<>.{name}");
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
