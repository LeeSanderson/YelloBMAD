namespace Yello.Client.Spaces;

/// <summary>
/// The Roles in rank order, Owner highest. A drop is a change to a lower rank.
/// </summary>
/// <remarks>
/// A rank compare is enough because the Roles' capability sets nest - each holds everything the one below holds - which
/// the Architecture suite proves against the server's matrix, so every drop only ever removes affordances.
/// </remarks>
public static class RoleRanks
{
    private const int Owner = 3;
    private const int Admin = 2;
    private const int Member = 1;
    private const int Viewer = 0;
    private const int Unknown = -1;

    /// <summary>
    /// The rank of a Role's stable code, or <c>-1</c> for a code the client does not know.
    /// </summary>
    /// <param name="roleCode">The stable code.</param>
    /// <returns>The rank.</returns>
    public static int RankOf(string roleCode) => roleCode switch
    {
        "owner" => Owner,
        "admin" => Admin,
        "member" => Member,
        "viewer" => Viewer,
        _ => Unknown,
    };
}
