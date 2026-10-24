using System.Globalization;

namespace Yello.Client.Spaces;

/// <summary>
/// Which of the client's own strings a Role is rendered with, and the context bar's accessible
/// name. AC5, AC6; UX-DR28, UX-DR37.
/// </summary>
/// <remarks>
/// <para>
/// <b>The Role arrives as its stable code</b> (<c>owner</c>, <c>admin</c>, <c>member</c>,
/// <c>viewer</c>) and is rendered from the client's copy: <c>Role{Name}</c> for the chip and
/// <c>RolePermits{Name}</c> for what it permits. Derived rather than tabled, as the refusal codes
/// are, and <c>SpaceSwitcherGateTests.Every_Role_stable_code_has_a_name_and_a_permit_phrase</c>
/// fails the build on a Role with no copy behind it.
/// </para>
/// <para>
/// <b>Sentence case in the resource, always.</b> The chip is uppercased by <c>text-transform</c>
/// (<c>.type-role-label</c>), so a screen reader hears "Admin", not "A-D-M-I-N", and the Turkish,
/// Azeri and Greek exclusions in <c>base.css</c> can withhold the transform.
/// </para>
/// </remarks>
public static class RoleCopy
{
    /// <summary>
    /// The context bar's accessible-name format: Space, Role, what it permits.
    /// </summary>
    public static string AccessibleNameKey => "ContextBarAccessibleName";

    /// <summary>
    /// The announcement made after a switch: the destination's Space, Role and what it permits.
    /// </summary>
    public static string SwitchAnnouncementKey => "SpaceSwitchAnnouncement";

    /// <summary>
    /// The resource key a Role's name is rendered from.
    /// </summary>
    /// <param name="roleCode">The Role's stable code.</param>
    /// <returns>The key.</returns>
    public static string NameKey(string roleCode) => "Role" + Pascal(roleCode);

    /// <summary>
    /// The resource key a Role's permit phrase is rendered from.
    /// </summary>
    /// <param name="roleCode">The Role's stable code.</param>
    /// <returns>The key.</returns>
    public static string PermitsKey(string roleCode) => "RolePermits" + Pascal(roleCode);

    /// <summary>
    /// Composes "{Space} — {Role}, {permits}" from the copy.
    /// </summary>
    /// <param name="copy">Resolves a resource key to its value.</param>
    /// <param name="formatKey">Which format: the accessible name or the switch announcement.</param>
    /// <param name="spaceName">The Space's name, from the server's answer.</param>
    /// <param name="roleCode">The Role's stable code, from the same answer.</param>
    /// <returns>The composed sentence.</returns>
    public static string Compose(Func<string, string> copy, string formatKey, string spaceName, string roleCode)
    {
        ArgumentNullException.ThrowIfNull(copy);

        return string.Format(
            CultureInfo.CurrentCulture,
            copy(formatKey),
            spaceName,
            copy(NameKey(roleCode)),
            copy(PermitsKey(roleCode)));
    }

    private static string Pascal(string code) =>
        string.Concat((code ?? string.Empty)
            .Split('-', StringSplitOptions.RemoveEmptyEntries)
            .Select(part => char.ToUpperInvariant(part[0]) + part[1..]));
}
