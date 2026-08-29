using System.Globalization;

namespace Yello.Contracts.Localisation;

/// <summary>
/// Chooses which of Yello's cultures serves a request or a browser.
/// </summary>
/// <remarks>
/// <para>
/// <b>Stated once because two surfaces answer it.</b> The client picks the culture it renders in
/// and writes onto <c>&lt;html lang&gt;</c>; the Host picks the culture it composes a Personal
/// Space's name in. If the two used different matching rules a browser asking for
/// <c>de-AT</c> could be served an English interface containing a German Space name, or the
/// reverse - a defect nobody would look for because each side would be behaving correctly on its
/// own terms.
/// </para>
/// <para>
/// The Host reaches the same answer through ASP.NET Core's request-localization middleware, which
/// implements this matching itself; what makes the two agree is that both are configured from
/// <see cref="SupportedCultures"/>. This class is where the rule is written down and where it is
/// tested.
/// </para>
/// <para>
/// <b>The rule, in order:</b> an exact culture match, then the language without its region, then
/// the default. The middle step is the one that matters in practice - almost every browser sends
/// a specific culture like <c>en-GB</c> or <c>de-CH</c>, and a resource set is almost always
/// neutral, so an exact-match-only rule would fall back to English for very nearly everybody.
/// </para>
/// </remarks>
public static class CultureSelection
{
    /// <summary>
    /// Resolves a requested culture to one Yello has resources for.
    /// </summary>
    /// <param name="requested">The culture the browser or request asked for.</param>
    /// <param name="supported">
    /// The cultures with resources behind them, normally <see cref="SupportedCultures.All"/>.
    /// </param>
    /// <param name="fallback">
    /// The culture to serve when nothing matches, normally
    /// <see cref="SupportedCultures.Default"/>.
    /// </param>
    /// <returns>The name of the culture to serve.</returns>
    public static string Resolve(
        CultureInfo requested,
        IReadOnlyList<string> supported,
        string fallback)
    {
        // Ordinal, never InvariantCultureIgnoreCase - the latter is a banned API here, and
        // culture names are machine identifiers rather than text, so an ordinal comparison is
        // also the correct one. OrdinalIgnoreCase because BCP 47 tags are case-insensitive:
        // a browser may send `EN-GB`.
        var exact = supported.FirstOrDefault(
            candidate => candidate.Equals(requested.Name, StringComparison.OrdinalIgnoreCase));

        if (exact is not null)
        {
            return exact;
        }

        // The language without its region. `de-AT` is served by `de` resources, which is what a
        // translator means by translating into German.
        var language = supported.FirstOrDefault(
            candidate => candidate.Equals(
                requested.TwoLetterISOLanguageName,
                StringComparison.OrdinalIgnoreCase));

        return language ?? fallback;
    }
}
