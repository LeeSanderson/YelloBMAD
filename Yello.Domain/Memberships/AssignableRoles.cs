namespace Yello.Domain.Memberships;

/// <summary>
/// The Roles a request may name for a Membership - by stable code, every Role but Owner. FR-10, FR-13, FR-42; stories 4.1
/// (the Role an Invitation offers) and 4.4 (the Role a change asks for, Q10 (a)).
/// </summary>
/// <remarks>
/// <b>Owner is never assignable by a request</b>: ownership moves only by an Ownership Offer (FR-42, story 5.2). A code
/// that names no Role, <c>owner</c> in any case, or a missing code is refused before anything is read. Codes compare
/// ordinally, so <c>OWNER</c> and <c>Admin</c> name nothing.
/// </remarks>
public static class AssignableRoles
{
    private static readonly Dictionary<string, Role> ByCode = Enum.GetValues<Role>()
        .Where(role => role != Role.Owner)
        .ToDictionary(role => StableCode.Of(role), role => role, StringComparer.Ordinal);

    /// <summary>
    /// Reads a Role a request names.
    /// </summary>
    /// <param name="code">The stable code as sent, or <c>null</c>.</param>
    /// <param name="role">The Role, when it reads.</param>
    /// <returns><c>false</c> for <c>owner</c>, an unknown code or none.</returns>
    public static bool TryParse(string? code, out Role role)
    {
        role = default;

        return code is not null && ByCode.TryGetValue(code, out role);
    }
}
