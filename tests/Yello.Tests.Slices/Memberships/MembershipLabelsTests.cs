using System.Reflection;
using System.Text.RegularExpressions;
using Xunit;

namespace Yello.Tests.Slices.Memberships;

/// <summary>
/// Story 4.4 AC3 (UX-DR34, <c>EXPERIENCE.md:151-155</c>) - the rule that two identical display names are disambiguated by a
/// Membership-scoped discriminator and never by the email address, in its one implementation: a pure function shared by the
/// client and the server, <c>Yello.Contracts.Memberships.MembershipLabels</c> (Q13 (a)). It answers "Sam Lee" alone, or
/// "Sam Lee · 3F2A" - the first four hex characters of a SHA-256 of the <c>MembershipId</c>, upper case - only when two
/// names in the same Space collide.
/// </summary>
/// <remarks>
/// <para>
/// <b>Written as red-phase scaffolds by the story 4.4 ATDD run (2026-11-02).</b> The type does not exist yet, so each case
/// reaches it by reflection (<see cref="Disambiguate"/>, <see cref="Initials"/>) and fails, when activated, on "not
/// declared". Once Task 3 declares it, the dev phase may swap the reflection for direct calls; the cases' expectations stay.
/// If a gate forbids logic in <c>Yello.Contracts</c>, Q13 moves the function to the client and these cases follow it
/// (recorded in the Debug Log).
/// </para>
/// <para>
/// <b>Why the reason is stated</b> (AC3's second clause, quoted from <c>EXPERIENCE.md:151</c>): "20px monospace initials
/// and a display name do not disambiguate two people, and the field every implementer reaches for to disambiguate a
/// Membership picker is the address." The function takes no address at all, so it cannot reach for one.
/// </para>
/// </remarks>
[Trait("Suite", "Slices")]
[Trait("Priority", "P1")]
[Trait("Requirement", "UX-DR34")]
public sealed class MembershipLabelsTests
{
    private const string LabelsTypeName = "Yello.Contracts.Memberships.MembershipLabels";

    private static readonly Guid First = Guid.Parse("0d9b3f4e-6c1a-4a8e-9f0b-1c2d3e4f5a6b");
    private static readonly Guid Second = Guid.Parse("7e1f2a3b-4c5d-4e6f-8a9b-0c1d2e3f4a5b");
    private static readonly Guid Third = Guid.Parse("a1b2c3d4-e5f6-4071-8293-a4b5c6d7e8f9");

    /// <summary>
    /// AC3 - two Memberships named "Sam Lee" get two distinct labels, each the name, a middle dot and four upper-case hex
    /// characters; a unique name in the same Space gets none.
    /// </summary>
    [Fact]
    public void Two_identical_names_get_two_distinct_suffixes_and_a_unique_name_gets_none()
    {
        var labels = Disambiguate([(First, "Sam Lee"), (Second, "Sam Lee"), (Third, "Ana Diaz")]);

        Assert.Equal("Ana Diaz", labels[Third]);
        Assert.Matches(Suffixed("Sam Lee"), labels[First]);
        Assert.Matches(Suffixed("Sam Lee"), labels[Second]);
        Assert.NotEqual(labels[First], labels[Second]);
    }

    /// <summary>
    /// AC3, the 4.1 Q3 normalisation family - names are compared after NFC: a composed and a decomposed "José" are one name
    /// (both suffixed). Case is folded too; the look-alike theory below holds that.
    /// </summary>
    [Fact]
    public void Names_are_compared_after_NFC()
    {
        var byForm = Disambiguate([(First, "José"), (Second, "José")]);

        Assert.All(byForm.Values, label => Assert.Contains(" · ", label, StringComparison.Ordinal));
    }

    /// <summary>
    /// AC3 - the suffix is stable: the same Memberships, asked twice and in reverse order, get the same labels. A reader's
    /// "Sam Lee · 3F2A" never silently becomes someone else (the reason Q13 rejects a join-order ordinal).
    /// </summary>
    [Fact]
    public void The_suffix_is_stable_across_calls_and_orderings()
    {
        (Guid, string)[] members = [(First, "Sam Lee"), (Second, "Sam Lee"), (Third, "Sam Lee")];

        var once = Disambiguate(members);
        var again = Disambiguate([.. members.Reverse()]);

        Assert.Equal(once.OrderBy(pair => pair.Key), again.OrderBy(pair => pair.Key));
    }

    /// <summary>
    /// AC2, AC3 - the discriminator never carries an <c>@</c> or a fragment of an address: over 200 random Memberships whose
    /// names all collide, every suffix is exactly four upper-case hex characters. The function's only inputs are the
    /// Membership id and the display name (its signature is checked), so no address can reach it. Plant 13: the
    /// discriminator built from the address.
    /// </summary>
    [Fact]
    public void The_suffix_never_carries_an_at_sign_or_a_fragment_of_an_address()
    {
        var method = DisambiguateMethod();
        var parameter = Assert.Single(method.GetParameters());

        Assert.DoesNotContain("Email", parameter.ParameterType.ToString(), StringComparison.Ordinal);

        var members = Enumerable.Range(0, 200).Select(_ => (Guid.NewGuid(), "Sam Lee")).ToList();
        var labels = Disambiguate(members);

        Assert.Equal(200, labels.Count);
        Assert.All(labels.Values, label =>
        {
            Assert.DoesNotContain("@", label, StringComparison.Ordinal);
            Assert.Matches(Suffixed("Sam Lee"), label);
        });
    }

    /// <summary>
    /// AC3, DESIGN.md <c>avatar</c> - the monospace initials the avatar shows are derived from the display name alone: the
    /// first letter of the first and last words, upper case.
    /// </summary>
    /// <param name="displayName">The display name.</param>
    /// <param name="expected">The initials.</param>
    [Theory]
    [InlineData("Sam Lee", "SL")]
    [InlineData("priya raman", "PR")]
    [InlineData("Ada", "A")]
    public void Initials_come_from_the_display_name_alone(string displayName, string expected) =>
        Assert.Equal(expected, Initials(displayName));

    /// <summary>
    /// Look-alike names collide: the collision key is the name after NFC, with
    /// default-ignorable format characters (category <c>Cf</c>, the zero-width space among them) removed, trimmed, with runs of
    /// whitespace collapsed to one space, compared ordinal-ignore-case. Each pair here reads as one name at avatar size, so
    /// both get a discriminator, and neither label carries an <c>@</c>.
    /// </summary>
    /// <param name="first">One display name.</param>
    /// <param name="second">A look-alike of it.</param>
    [Theory]
    [InlineData("Ann", "Ann ")]
    [InlineData("Ann", " Ann")]
    [InlineData("Ann", "A​nn")]
    [InlineData("ann", "Ann")]
    [InlineData("Ann  Lee", "Ann Lee")]
    [InlineData("Ann\tLee", "Ann Lee")]
    [Trait("Requirement", "AD-23")]
    public void Look_alike_names_collide_and_both_get_a_discriminator(string first, string second)
    {
        var labels = Disambiguate([(First, first), (Second, second)]);

        Assert.Matches(Suffixed(first), labels[First]);
        Assert.Matches(Suffixed(second), labels[Second]);
        Assert.NotEqual(labels[First], labels[Second]);
        Assert.All(labels.Values, label => Assert.DoesNotContain("@", label, StringComparison.Ordinal));
    }

    /// <summary>
    /// Only the comparison folds: a colliding name is shown exactly as its owner wrote it, case, spacing and all, followed by
    /// the discriminator.
    /// </summary>
    [Fact]
    [Trait("Requirement", "AD-23")]
    public void A_folded_collision_keeps_each_name_as_written()
    {
        var labels = Disambiguate([(First, "sam  lee"), (Second, "Sam Lee")]);

        Assert.StartsWith("sam  lee · ", labels[First], StringComparison.Ordinal);
        Assert.StartsWith("Sam Lee · ", labels[Second], StringComparison.Ordinal);
    }

    /// <summary>
    /// The fold is not a prefix or a similarity match: "Ann" and "Anne" are two names, and neither gets a discriminator. Green
    /// today and after the fold; it keeps the widened key from over-reaching.
    /// </summary>
    [Fact]
    [Trait("Requirement", "AD-23")]
    public void Different_names_that_share_a_prefix_do_not_collide()
    {
        var labels = Disambiguate([(First, "Ann"), (Second, "Anne")]);

        Assert.Equal(("Ann", "Anne"), (labels[First], labels[Second]));
    }

    private static Regex Suffixed(string name) => new("^" + Regex.Escape(name) + " · [0-9A-F]{4}$", RegexOptions.None, TimeSpan.FromSeconds(1));

    private static Type LabelsType()
    {
        var type = typeof(Yello.Contracts.AssemblyMarker).Assembly.GetType(LabelsTypeName);

        Assert.True(type is not null, $"{LabelsTypeName} is not declared (Q13 (a)).");

        return type;
    }

    private static MethodInfo DisambiguateMethod()
    {
        var method = LabelsType().GetMethod("Disambiguate", BindingFlags.Public | BindingFlags.Static);

        Assert.True(method is not null, $"{LabelsTypeName}.Disambiguate is not declared.");

        return method;
    }

    private static IReadOnlyDictionary<Guid, string> Disambiguate(IReadOnlyList<(Guid MembershipId, string DisplayName)> members) =>
        Assert.IsAssignableFrom<IReadOnlyDictionary<Guid, string>>(DisambiguateMethod().Invoke(null, [members]));

    private static string Initials(string displayName)
    {
        var method = LabelsType().GetMethod("Initials", BindingFlags.Public | BindingFlags.Static);

        Assert.True(method is not null, $"{LabelsTypeName}.Initials is not declared.");

        return Assert.IsType<string>(method.Invoke(null, [displayName]));
    }
}
