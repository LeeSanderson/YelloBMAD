using System.Globalization;
using Microsoft.Extensions.Localization;
using Yello.Client.Localisation;
using Yello.Contracts;

namespace Yello.Client.Refusals;

/// <summary>
/// The words a refusal is rendered with inline, in a creation or a rename surface: <see cref="RefusalCopy"/>'s
/// key, formatted with the bound's number when it is a bound refusal. Stories 2.1 and 2.2.
/// </summary>
/// <remarks>
/// One copy for every inline surface - the Project list's and the Board's - so a refusal reads the same
/// wherever it is met (UX-DR32, UX-DR37).
/// </remarks>
public static class RefusalText
{
    /// <summary>
    /// The text for a refusal.
    /// </summary>
    /// <param name="localizer">The client's copy.</param>
    /// <param name="problem">The problem body, or <c>null</c>.</param>
    /// <returns>The text to render.</returns>
    public static string Of(IStringLocalizer<ClientCopy> localizer, ProblemResponse? problem)
    {
        ArgumentNullException.ThrowIfNull(localizer);

        var message = RefusalCopy.For(problem, key => !localizer[key].ResourceNotFound);

        return message.Limit is { } limit
            ? string.Format(CultureInfo.CurrentCulture, localizer[message.Key], limit)
            : localizer[message.Key];
    }
}
