namespace Yello.Contracts.Localisation;

/// <summary>
/// The cultures Yello actually has translations for, stated once for both surfaces.
/// </summary>
/// <remarks>
/// <para>
/// <b>It lives in <c>Yello.Contracts</c> because both sides need the same answer.</b> The client
/// picks the culture it renders in and writes it onto <c>&lt;html lang&gt;</c>; the Host resolves
/// the culture it composes a Personal Space's name in from the request's
/// <c>Accept-Language</c>. Two independent lists would let the two disagree about what a browser
/// asking for German gets, and the visible symptom would be a Space named in one language inside
/// an interface rendered in another.
/// </para>
/// <para>
/// <b>One entry, and that is a statement of fact rather than a placeholder.</b> There is exactly
/// one set of resources in the repository today and it is neutral English. Listing locales with
/// no translation behind them would make <c>base.css</c>'s casing exclusions appear exercised
/// while every string still rendered in English - which is the shape of the inert-check defect
/// this suite exists to catch.
/// </para>
/// <para>
/// <b>Adding a translation means adding it here.</b> That is the deliberate coupling: a
/// <c>.de.resx</c> that nobody adds to this list reaches no one, and a locale added here with no
/// resources behind it falls back to English while claiming to be supported.
/// </para>
/// <para>
/// <b>What actually asserts that, corrected at code review on 2026-09-10.</b> This paragraph used
/// to name <c>SupportedCulturesTests</c>, which does not exist and never has. Two gates in
/// <c>LocalisationGateTests</c> cover it, and they cover different amounts:
/// <c>Every_supported_culture_has_resources_behind_it</c> holds the "listed here, resources
/// missing" direction - though on a one-entry list its own <c>where</c> filter excludes the only
/// entry, so what it asserts today is that the neutral set exists; and
/// <c>The_supported_culture_list_has_exactly_one_entry</c> fails the day a second is added, with
/// the reason in its message. The reverse direction - a satellite <c>.resx</c> present in the
/// assembly with no entry here - is asserted by nothing, and that is recorded in
/// <c>deferred-work.md</c> rather than claimed here.
/// </para>
/// </remarks>
public static class SupportedCultures
{
    /// <summary>
    /// The culture used when the request asks for nothing Yello has, and the culture the neutral
    /// resources are written in.
    /// </summary>
    public static string Default => "en";

    /// <summary>
    /// Every culture Yello has resources for, the default first.
    /// </summary>
    public static IReadOnlyList<string> All { get; } = [Default];
}
