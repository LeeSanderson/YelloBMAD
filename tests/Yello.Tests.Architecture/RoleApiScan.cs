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

    /// <summary>Any Identity type whose name mentions Role is role-API surface.</summary>
    private static readonly Regex IdentityRoleType =
        new(@"^Microsoft\.AspNetCore\.Identity(\.\w+)*\.I?IdentityRole", RegexOptions.Compiled);

    /// <summary>
    /// The role store and its manager, in every generic arity, plus the EF Core role store
    /// implementation.
    /// </summary>
    private static readonly Regex RoleStoreType =
        new(@"^Microsoft\.AspNetCore\.Identity(\.\w+)*\.I?Role(Manager|Store|Validator)", RegexOptions.Compiled);

    private static readonly Lazy<ScanResult> Result = new(Scan, isThreadSafe: true);

    /// <summary>Sites applying <c>[Authorize(Roles = ...)]</c>.</summary>
    public static IReadOnlyList<string> AuthorizeRolesUsages => Result.Value.AuthorizeRoles;

    /// <summary>Call sites invoking <c>IsInRole</c> on a principal.</summary>
    public static IReadOnlyList<string> IsInRoleCalls => Result.Value.IsInRoleCalls;

    /// <summary>References to <c>IdentityRole</c> in any arity.</summary>
    public static IReadOnlyList<string> IdentityRoleReferences => Result.Value.IdentityRoleReferences;

    /// <summary>References to Identity's role store or role manager.</summary>
    public static IReadOnlyList<string> RoleStoreReferences => Result.Value.RoleStoreReferences;

    private sealed record ScanResult(
        List<string> AuthorizeRoles,
        List<string> IsInRoleCalls,
        List<string> IdentityRoleReferences,
        List<string> RoleStoreReferences);

    private static ScanResult Scan()
    {
        var result = new ScanResult([], [], [], []);

        foreach (var assembly in ProductionAssemblies.All)
        {
            using var module = ModuleDefinition.ReadModule(assembly.Location);
            var assemblyName = module.Assembly.Name.Name;

            foreach (var typeReference in module.GetTypeReferences())
            {
                var fullName = typeReference.FullName;

                if (IdentityRoleType.IsMatch(fullName))
                {
                    result.IdentityRoleReferences.Add($"{assemblyName}: references {fullName}");
                }

                if (RoleStoreType.IsMatch(fullName))
                {
                    result.RoleStoreReferences.Add($"{assemblyName}: references {fullName}");
                }
            }

            foreach (var type in module.GetTypes())
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
                    var location = $"{assemblyName}: {type.FullName}.{method.Name}";

                    CollectAuthorizeRoles(method.CustomAttributes, $"{assemblyName}: method {location}", result);

                    if (!method.HasBody)
                    {
                        continue;
                    }

                    foreach (var instruction in method.Body.Instructions)
                    {
                        if (instruction.Operand is MethodReference called
                            && called.Name.Equals("IsInRole", StringComparison.Ordinal))
                        {
                            result.IsInRoleCalls.Add($"{location} calls {called.DeclaringType.FullName}.IsInRole");
                        }
                    }
                }
            }
        }

        return result;
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
                && attribute.Properties.Any(p => p.Name.Equals("Roles", StringComparison.Ordinal)))
            {
                result.AuthorizeRoles.Add($"{location} is annotated [Authorize(Roles = ...)]");
            }
        }
    }
}
