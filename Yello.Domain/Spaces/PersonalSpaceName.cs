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
/// function and the one resource string the template comes from - not the slice and not the
/// schema.
/// </para>
/// <para>
/// <b>What the tests do and do not reach, corrected at code review on 2026-09-10.</b> This
/// paragraph used to end "not the tests, which assert against the composed value rather than
/// against a spelling". That was not true: every test double is a fake that composes the suffix
/// itself, so the assertions compared a fake against its own output and no test constructed
/// <c>ResourcePersonalSpaceNaming</c> or read the resource at all. A renamed key, a lost embedded
/// resource or an added <c>ResourcesPath</c> would have named every Space in production
/// <c>"PersonalSpaceName"</c> - the key echoed back by <c>IStringLocalizer</c>, which returns the
/// key rather than throwing - with the whole suite green.
/// <c>ResourcePersonalSpaceNamingTests</c> now constructs the real adapter over the real resource
/// set; <see cref="From"/> refuses a template that does not place the name.
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
    /// The template is not a usable composite format string, or does not place the display name
    /// in its result. Thrown rather than swallowed: a malformed resource would otherwise name
    /// every Space after the exception's own text, and a placeholder-less one would name every
    /// Space after the template.
    /// </exception>
    public static string From(string displayName, string template)
    {
        var name = displayName.Trim();

        string composed;

        try
        {
            composed = string.Format(CultureInfo.CurrentCulture, template, name);
        }
        catch (FormatException exception)
        {
            throw new ArgumentException(
                $"The Personal Space name template '{template}' is not a usable composite format " +
                "string. It must contain exactly one placeholder, '{0}', for the display name.",
                nameof(template),
                exception);
        }

        // A template with NO placeholder is not a FormatException - string.Format returns it
        // unchanged - so the failure this method exists to catch used to pass straight through.
        // That is not a hypothetical shape: it is exactly what a missing resource produces, since
        // IStringLocalizer answers a key it cannot find with the key itself. Every Space would
        // have been called "PersonalSpaceName". Checking the result rather than parsing the
        // template also catches "{{0}}'s Space", where the placeholder is escaped rather than
        // absent.
        //
        // Vacuous when the name is empty, which is honest: Contains("") is true. The slice cannot
        // reach that - RegisterAccountValidator refuses a blank display name before the handler
        // runs - and guarding it here would be a rule with no caller.
        if (name.Length > 0 && !composed.Contains(name, StringComparison.Ordinal))
        {
            throw new ArgumentException(
                $"The Personal Space name template '{template}' does not place the display name " +
                "in its result. It must contain exactly one placeholder, '{0}', for the display " +
                "name - an unplaceheld template names every Space after the template itself.",
                nameof(template));
        }

        return composed;
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
