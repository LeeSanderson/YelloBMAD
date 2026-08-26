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
/// </remarks>
[Trait("Suite", "Architecture")]
[Trait("Priority", "P0")]
[Trait("Requirement", "AR-4")]
public sealed class RoleApiBanTests
{
    /// <summary>
    /// A-3.1
    /// </summary>
    [Fact]
    public void No_code_applies_Authorize_with_a_Roles_argument()
    {
        AssertNoUsages(
            RoleApiScan.AuthorizeRolesUsages,
            "[Authorize(Roles = ...)] is banned. Authorisation is a function of (Account, Space) " +
            "through a Membership, so a role carried on the principal cannot express it - the same " +
            "Account holds different Roles in different Spaces. Authorise against the resolved " +
            "Space instead. [Authorize] with a policy is not banned; the Roles argument is.");
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
            "IdentityRole is banned. Yello's Role is an attribute of a Membership - the join " +
            "between an Account and a Space - and never a row in an Identity role table.");
    }

    /// <summary>
    /// A-3.4
    /// </summary>
    [Fact]
    public void No_code_references_Identitys_role_store_or_role_manager()
    {
        AssertNoUsages(
            RoleApiScan.RoleStoreReferences,
            "Identity's role store (RoleManager<>, IRoleStore<>) is banned. Identity is wired for " +
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
