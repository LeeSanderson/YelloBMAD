namespace Yello.Tests.Shared;

/// <summary>
/// The both-surfaces gap gate, generic over any case list (AC1, AD-4): a case that states what one
/// surface gets and not another is a gap, not a choice.
/// </summary>
public static class SurfaceGaps
{
    /// <summary>
    /// "Case id / surface" for every surface a case declares no expectation for.
    /// </summary>
    /// <typeparam name="TCase">The case type.</typeparam>
    /// <typeparam name="TExpectation">The expectation type.</typeparam>
    /// <param name="cases">The list.</param>
    /// <param name="id">A case's id.</param>
    /// <param name="expectations">A case's expectations by surface.</param>
    /// <returns>The gaps, empty when there are none.</returns>
    public static IReadOnlyList<string> Find<TCase, TExpectation>(
        IEnumerable<TCase> cases,
        Func<TCase, string> id,
        Func<TCase, IReadOnlyDictionary<IsolationSurface, TExpectation>> expectations) =>
        [.. cases.SelectMany(@case => Enum.GetValues<IsolationSurface>()
            .Where(surface => !expectations(@case).ContainsKey(surface))
            .Select(surface => $"{id(@case)} / {surface}"))];
}
