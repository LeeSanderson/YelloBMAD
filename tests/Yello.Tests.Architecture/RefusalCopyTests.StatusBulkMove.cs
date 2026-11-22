using System.Collections;
using System.Globalization;
using System.Text.RegularExpressions;
using Xunit;

namespace Yello.Tests.Architecture;

/// <summary>
/// The client's copy for moving every Task of one Status into another Project: each state the bar says has its words, in the
/// singular and the plural, and the two refusals that are the bulk move's own say that nothing moved.
/// </summary>
/// <remarks>
/// The strings are matched by value with their placeholders numbered away, so the keys and the placeholder order are the
/// implementation's to choose.
/// </remarks>
public sealed partial class RefusalCopyTests
{
    /// <summary>
    /// The bar's scope, in-flight line and announcement exist in the singular and the plural; the full-destination and the
    /// gone-Status refusals end "Nothing was moved."
    /// </summary>
    /// <param name="expected">The copy, with each placeholder written <c>{}</c>.</param>
    [Theory]
    [InlineData("{} Tasks in “{}”.")]
    [InlineData("1 Task in “{}”.")]
    [InlineData("Moving {} Tasks.")]
    [InlineData("Moving 1 Task.")]
    [InlineData("Moved {} Tasks to {}.")]
    [InlineData("Moved 1 Task to {}.")]
    [InlineData("Moved {} Tasks to {}, in {}.")]
    [InlineData("Moved 1 Task to {}, in {}.")]
    [InlineData("{} cannot take {} more Tasks (at most {}). Nothing was moved.")]
    [InlineData("“{}” is no longer in this Project. Nothing was moved.")]
    [Trait("Requirement", "UX-DR17")]
    [Trait("Requirement", "UX-DR32")]
    public void The_bulk_move_copy_is_in_the_client_copy(string expected)
    {
        var values = Copy.GetResourceSet(CultureInfo.InvariantCulture, createIfNotExists: true, tryParents: true)!
            .Cast<DictionaryEntry>()
            .Select(entry => Placeholder.Replace(entry.Value as string ?? string.Empty, "{}"))
            .ToList();

        Assert.Contains(expected, values);
    }

    [GeneratedRegex(@"\{\d+(?:[,:][^}]*)?\}", RegexOptions.None, matchTimeoutMilliseconds: 5000)]
    private static partial Regex Placeholder { get; }
}
