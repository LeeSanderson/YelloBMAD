using Xunit;

namespace Yello.Tests.Architecture;

/// <summary>
/// Gate C - the Role-API ban (AC3). A-3's four assertions, and the substance of what story
/// 1.1 delivers: nothing here wires Identity, so the ban is the deliverable.
/// </summary>
/// <remarks>
/// <para>
/// <b>ASP.NET Core Identity remains permitted, for authentication only</b> - the Account
/// store, password hashing and cookie issuance. What is banned is Identity's
/// <i>authorisation</i> surface: roles. The reason is settled and not to be reopened:
/// authorisation in Yello is a function of <c>(Account, Space)</c> resolved through a
/// many-to-many <c>Membership</c>, not a property of an Account. A role claim carried on a
/// principal cannot express that, because the same Account holds different Roles in
/// different Spaces - and a principal has one set of roles at a time.
/// </para>
/// <para>
/// The related rejected shape, for the same reason: a generic <c>TenantId</c> column filter
/// or ambient tenant middleware. Also settled, also not to be reopened.
/// </para>
/// <para>
/// Like Gate B, all four assertions are absence assertions and are vacuously true against
/// story 1.1's empty projects. Each was validated against a planted violation in Task 9; the
/// results are recorded in the story's Dev Agent Record.
/// </para>
/// <para>
/// <b>Scope is the whole solution</b> - AC3's words - which includes <c>tests/**</c>. See
/// <see cref="SolutionAssemblies"/> for how the suites are reached without referencing them,
/// and <see cref="RoleApiScan"/> for which forms of role authorisation each assertion covers.
/// </para>
/// </remarks>
[Trait("Suite", "Architecture")]
[Trait("Priority", "P0")]
[Trait("Requirement", "AR-4")]
public sealed class RoleApiBanTests
{
    /// <summary>
    /// The gate's own precondition: it can only ban what it can read.
    /// </summary>
    /// <remarks>
    /// AC3 says "anywhere in the solution", so a scan that silently covered eight of fourteen
    /// assemblies would satisfy its four assertions while leaving six unexamined. Failing here
    /// makes that visible instead.
    /// </remarks>
    [Fact]
    public void The_scan_can_read_every_assembly_in_the_solution()
    {
        Assert.True(SolutionAssemblies.Unreadable.Count == 0,
            "Gate C could not find these compiled assemblies, so it did NOT scan them - and " +
            "AC3 bans the Role API anywhere in the solution, not anywhere the gate happened to " +
            $"look:{Environment.NewLine}" +
            string.Join(Environment.NewLine, SolutionAssemblies.Unreadable.Select(a => $"  - {a}")) +
            $"{Environment.NewLine}{Environment.NewLine}" +
            "Build the whole solution (`dotnet build Yello.slnx`) before running this suite.");
    }

    /// <summary>
    /// A-3.1
    /// </summary>
    [Fact]
    public void No_code_applies_Authorize_with_a_Roles_argument()
    {
        AssertNoUsages(
            RoleApiScan.AuthorizeRolesUsages,
            "Role-based authorisation is banned in every form: [Authorize(Roles = ...)] and any " +
            "subclass of it, `new AuthorizeAttribute { Roles = ... }` as an object initialiser, " +
            "AuthorizationPolicyBuilder.RequireRole, and RolesAuthorizationRequirement. " +
            "Authorisation is a function of (Account, Space) through a Membership, so a role " +
            "carried on the principal cannot express it - the same Account holds different Roles " +
            "in different Spaces. Authorise against the resolved Space instead. [Authorize] with " +
            "a policy is not banned; roles are - including a policy built out of RequireRole.");
    }

    /// <summary>
    /// A-3.2
    /// </summary>
    [Fact]
    public void No_code_calls_IsInRole_on_a_principal()
    {
        AssertNoUsages(
            RoleApiScan.IsInRoleCalls,
            "ClaimsPrincipal.IsInRole is banned. A principal carries one set of roles at a time, " +
            "which cannot represent an Account whose Role differs per Space. Note that " +
            "ClaimsPrincipal itself is permitted - Identity stays wired for authentication - so " +
            "this ban is on the method, not the type.");
    }

    /// <summary>
    /// A-3.3
    /// </summary>
    [Fact]
    public void No_code_references_IdentityRole()
    {
        AssertNoUsages(
            RoleApiScan.IdentityRoleReferences,
            "Identity's role entity family is banned - IdentityRole, IdentityUserRole (the " +
            "account-to-role join, i.e. exactly the table this architecture rejects) and " +
            "IdentityRoleClaim, in any arity. Yello's Role is an attribute of a Membership - the " +
            "join between an Account and a Space - and never a row in an Identity role table.");
    }

    /// <summary>
    /// A-3.4
    /// </summary>
    [Fact]
    public void No_code_references_Identitys_role_store_or_role_manager()
    {
        AssertNoUsages(
            RoleApiScan.RoleStoreReferences,
            "Identity's role store is banned in every form - RoleManager<>, IRoleStore<>, " +
            "IRoleClaimStore<>, IRoleValidator<>, RoleStore<>, and the IdentityBuilder calls that " +
            "wire them (AddRoles<TRole>(), AddRoleManager<T>()). Identity is wired for " +
            "authentication ONLY: the Account store, password hashing and cookie issuance. Adding " +
            "the role store would introduce a second, competing model of who may do what.");
    }

    private static void AssertNoUsages(IReadOnlyCollection<string> usages, string why)
    {
        Assert.True(usages.Count == 0,
            $"{why}{Environment.NewLine}{Environment.NewLine}Found at:{Environment.NewLine}" +
            string.Join(Environment.NewLine, usages.Select(u => $"  - {u}")));
    }
}
