namespace Yello.Domain.Memberships;

/// <summary>
/// What an Account may do inside one Space. An attribute of a
/// <see cref="Membership"/>, and never anything else.
/// </summary>
/// <remarks>
/// <para>
/// <b>This is not, and may never become, an Identity role.</b> AD-1 wires ASP.NET Core Identity
/// for authentication only; Yello's Role is a column on <see cref="Membership"/>, never an
/// <c>IdentityRole</c>, never a claim and never a cookie value. Gate C
/// (<c>tests/Yello.Tests.Architecture/RoleApiBanTests.cs</c>) is a live IL scan over every
/// assembly in the solution that fails the build on <c>IdentityRole</c> in any arity,
/// <c>RoleManager&lt;&gt;</c>, <c>IRoleStore&lt;&gt;</c>, <c>AddRoles&lt;&gt;()</c>,
/// <c>[Authorize(Roles=...)]</c>, <c>RequireRole</c>, <c>ClaimsPrincipal.IsInRole</c>,
/// <c>ClaimTypes.Role</c> however it is spelled, and <c>UserManager&lt;&gt;</c>'s role surface.
/// </para>
/// <para>
/// The reason is structural rather than stylistic: the same Account holds different Roles in
/// different Spaces, so a single role claim on the principal cannot say which one applies. An
/// Identity role is not merely a different spelling of this enum - it is a model that cannot
/// express Yello's authorisation at all.
/// </para>
/// <para>
/// <b>Persisted as its name, not its number.</b> The configuration converts this to a string
/// column, so the ordinal values below never reach disk and reordering them is not a migration.
/// It also makes AD-5's filtered unique index - <c>WHERE Role = 'Owner'</c> - readable in the
/// database rather than a magic integer.
/// </para>
/// </remarks>
public enum Role
{
    /// <summary>
    /// The Space's owner. Exactly one Membership per Space carries this, enforced by a filtered
    /// unique index (AD-5 / AR-12) rather than by application code.
    /// </summary>
    Owner,

    /// <summary>
    /// Full control of the Space's contents, short of the four boundaries the role-capability
    /// matrix reserves to the Owner.
    /// </summary>
    Admin,

    /// <summary>
    /// Reads and writes the Space's Projects and Tasks.
    /// </summary>
    Member,

    /// <summary>
    /// Reads the Space's Projects and Tasks and changes nothing.
    /// </summary>
    Viewer,
}
