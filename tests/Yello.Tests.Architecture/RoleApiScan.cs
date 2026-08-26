using System.Text.RegularExpressions;
using Mono.Cecil;

namespace Yello.Tests.Architecture;

/// <summary>
/// Scans the compiled IL of every production assembly for the four banned Role-API shapes.
/// </summary>
/// <remarks>
/// <para>
/// Mono.Cecil rather than ArchUnitNET's fluent rules, because three of the four bans need a
/// precision the type-level API cannot express. The ban on
/// <c>ClaimsPrincipal.IsInRole</c> is a ban on one <i>method</i>: <c>ClaimsPrincipal</c>
/// itself is entirely legitimate here, since Identity stays wired for authentication. A rule
/// phrased as "must not depend on ClaimsPrincipal" would forbid the thing that is allowed
/// and still not name the thing that is not.
/// </para>
/// <para>
/// The scan runs once and is shared, so the four assertions each report against the same
/// pass over the bytecode.
/// </para>
/// </remarks>
internal static class RoleApiScan
{
    private const string AuthorizeAttributeFullName = "Microsoft.AspNetCore.Authorization.AuthorizeAttribute";

    /// <summary>
    /// Any Identity type whose name mentions Role is role-API surface.
    /// </summary>
    private static readonly Regex IdentityRoleType =
        new(@"^Microsoft\.AspNetCore\.Identity(\.\w+)*\.I?IdentityRole", RegexOptions.Compiled);

    /// <summary>
    /// The role store and its manager, in every generic arity, plus the EF Core role store
    /// implementation.
    /// </summary>
    private static readonly Regex RoleStoreType =
        new(@"^Microsoft\.AspNetCore\.Identity(\.\w+)*\.I?Role(Manager|Store|Validator)", RegexOptions.Compiled);

    private static readonly Lazy<ScanResult> Result = new(Scan, isThreadSafe: true);

    /// <summary>
    /// Sites applying <c>[Authorize(Roles = ...)]</c>.
    /// </summary>
    public static IReadOnlyList<string> AuthorizeRolesUsages => Result.Value.AuthorizeRoles;

    /// <summary>
    /// Call sites invoking <c>IsInRole</c> on a principal.
    /// </summary>
    public static IReadOnlyList<string> IsInRoleCalls => Result.Value.IsInRoleCallSites;

    /// <summary>
    /// References to <c>IdentityRole</c> in any arity.
    /// </summary>
    public static IReadOnlyList<string> IdentityRoleReferences => Result.Value.IdentityRoleTypes;

    /// <summary>
    /// References to Identity's role store or role manager.
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
    /// One pass over every production assembly.
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

        foreach (var assembly in ProductionAssemblies.All)
        {
            using var module = ModuleDefinition.ReadModule(assembly.Location);
            ScanModule(module, result);
        }

        return result;
    }

    private static void ScanModule(ModuleDefinition module, ScanResult result)
    {
        var assemblyName = module.Assembly.Name.Name;

        CollectRoleTypeReferences(module, assemblyName, result);

        foreach (var type in module.GetTypes())
        {
            ScanType(type, assemblyName, result);
        }
    }

    /// <summary>
    /// The two type-level bans, read from the module's type-reference table rather than from
    /// its members: a reference is a reference whether or not any code path reaches it.
    /// </summary>
    private static void CollectRoleTypeReferences(
        ModuleDefinition module,
        string assemblyName,
        ScanResult result)
    {
        foreach (var typeReference in module.GetTypeReferences())
        {
            var fullName = typeReference.FullName;

            if (IdentityRoleType.IsMatch(fullName))
            {
                result.IdentityRoleTypes.Add($"{assemblyName}: references {fullName}");
            }

            if (RoleStoreType.IsMatch(fullName))
            {
                result.RoleStoreTypes.Add($"{assemblyName}: references {fullName}");
            }
        }
    }

    private static void ScanType(TypeDefinition type, string assemblyName, ScanResult result)
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
            ScanMethod(method, assemblyName, result);
        }
    }

    private static void ScanMethod(MethodDefinition method, string assemblyName, ScanResult result)
    {
        var location = $"{assemblyName}: {method.DeclaringType.FullName}.{method.Name}";

        CollectAuthorizeRoles(method.CustomAttributes, $"{assemblyName}: method {location}", result);

        if (!method.HasBody)
        {
            return;
        }

        foreach (var instruction in method.Body.Instructions)
        {
            if (instruction.Operand is MethodReference called
                && called.Name.Equals("IsInRole", StringComparison.Ordinal))
            {
                result.IsInRoleCallSites.Add($"{location} calls {called.DeclaringType.FullName}.IsInRole");
            }
        }
    }

    private static void CollectAuthorizeRoles(
        IEnumerable<CustomAttribute> attributes,
        string location,
        ScanResult result)
    {
        foreach (var attribute in attributes)
        {
            if (!attribute.AttributeType.FullName.Equals(AuthorizeAttributeFullName, StringComparison.Ordinal))
            {
                continue;
            }

            // Roles is a settable property, so it arrives as a named argument rather than a
            // constructor argument - AuthorizeAttribute's only string constructor takes a
            // policy, which is the permitted shape.
            if (attribute.HasProperties
                && attribute.Properties.Any(property => property.Name.Equals("Roles", StringComparison.Ordinal)))
            {
                result.AuthorizeRoles.Add($"{location} is annotated [Authorize(Roles = ...)]");
            }
        }
    }
}
