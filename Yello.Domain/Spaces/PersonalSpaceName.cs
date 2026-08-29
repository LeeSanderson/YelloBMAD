using System.Globalization;

namespace Yello.Domain.Spaces;

/// <summary>
/// The single place a Personal Space's name is composed. One function, one resource string.
/// </summary>
/// <remarks>
/// <para>
/// <b>This exists as a choke point because the question it answers was contested.</b> Story 1.3
/// found a genuine contradiction across five documents rather than an ambiguity: three sources
/// require a display name for the Space (<c>prd.md:164</c>, <c>SPEC.md:268</c>,
/// <c>acceptance-criteria.md:49</c> - "named from the Account's display name, e.g. \"Ravi's
/// Space\"") and two more show the literal string, while five sources fix registration at exactly
/// two fields and nothing anywhere defined a display-name attribute. It is PRD section 12
/// assumption 1, one of the thirteen the readiness report records as hardened into acceptance
/// criteria without ever being confirmed.
/// </para>
/// <para>
/// <b>Lee resolved it on 2026-08-28 by confirming the assumption rather than revising it:</b>
/// registration collects a display name, and the Space is named from it. That is what the
/// readiness report's issue 5 asks for, and it is what UX-DR34 needs anyway - every Membership
/// rendering from Epic 4 onward requires a display name, and <c>EXPERIENCE.md:210</c> derives
/// avatar initials from it.
/// </para>
/// <para>
/// The structure survives the decision going the other way. Changing the answer changes this one
/// function and the one resource string the template comes from - not the slice, not the schema
/// and not the tests, which assert against the composed value rather than against a spelling.
/// </para>
/// <para>
/// <b>Nothing about the composed name is recorded on the row.</b> The Space stores a name like
/// any other Space's; no column says it was derived. That is AC4 and
/// <c>decisions-settled.md:26</c>: Personal Space is descriptive, not a type.
/// </para>
/// </remarks>
public static class PersonalSpaceName
{
    /// <summary>
    /// Composes the name of the Space provisioned for an Account.
    /// </summary>
    /// <param name="displayName">The Account's display name, as collected at registration.</param>
    /// <param name="template">
    /// A composite format string with one placeholder for the display name - for example
    /// <c>"{0}'s Space"</c>. It comes from a localisation resource through
    /// <see cref="IPersonalSpaceNaming"/> rather than being written here, because the possessive
    /// construction is different in every language and several have none at all.
    /// </param>
    /// <returns>The Space's name.</returns>
    /// <exception cref="ArgumentException">
    /// The template is not a usable composite format string. Thrown rather than swallowed: a
    /// malformed resource would otherwise name every Space after the exception's own text.
    /// </exception>
    public static string From(string displayName, string template)
    {
        try
        {
            return string.Format(CultureInfo.CurrentCulture, template, displayName.Trim());
        }
        catch (FormatException exception)
        {
            throw new ArgumentException(
                $"The Personal Space name template '{template}' is not a usable composite format " +
                "string. It must contain exactly one placeholder, '{0}', for the display name.",
                nameof(template),
                exception);
        }
    }
}

/// <summary>
/// Supplies <see cref="PersonalSpaceName"/> with its localised template.
/// </summary>
/// <remarks>
/// A port, because the template lives in a resource and resource lookup is an
/// <c>Yello.Infrastructure</c> concern. The slice asks for a name and is told one; it never sees
/// a format string, which is what keeps the naming decision behind a single edit.
/// </remarks>
public interface IPersonalSpaceNaming
{
    /// <summary>
    /// The name for the Space provisioned for an Account with this display name.
    /// </summary>
    string NameFor(string displayName);
}
