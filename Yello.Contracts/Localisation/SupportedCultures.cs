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
/// resources behind it falls back to English while claiming to be supported. The list and the
/// resource files are asserted against each other by
/// <c>SupportedCulturesTests</c> rather than left to whoever remembers.
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
