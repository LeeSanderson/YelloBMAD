using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Xunit;
using Yello.Infrastructure.Localisation;

namespace Yello.Tests.Slices.Accounts.RegisterAccount;

/// <summary>
/// The real naming adapter, over the real resource set. AC1, FR-4.
/// </summary>
/// <remarks>
/// <para>
/// <b>Written at code review on 2026-09-10, because nothing reached this code at all.</b> Every
/// other test of Personal Space naming supplies its own <c>IPersonalSpaceNaming</c> - a fake that
/// composes <c>displayName + "'s Space"</c> in the test itself - and then asserts the Space's name
/// equals what that fake produced. Three classes did it, and together they asserted a fake against
/// its own output while <see cref="ResourcePersonalSpaceNaming"/> and
/// <c>RegistrationCopy.resx</c> were executed by nothing.
/// </para>
/// <para>
/// <b>What that left open.</b> <see cref="IStringLocalizer"/> answers a key it cannot find with
/// the key itself - <c>ResourceNotFound</c> is set on the result, and nothing throws. So a renamed
/// key, a lost embedded resource, or a <c>ResourcesPath</c> added to the project would have made
/// <c>localizer["PersonalSpaceName"].Value</c> return the literal string
/// <c>"PersonalSpaceName"</c>, which <c>string.Format</c> then returns unchanged because it holds
/// no placeholder. Every Space in production would have been called <c>PersonalSpaceName</c>, with
/// all 136 tests green. That is this repository's named defect class - a check that reports
/// success while the thing it names is switched off - sitting on the one decision the story
/// records as contested across five documents.
/// </para>
/// <para>
/// No container: this reads an embedded resource and composes a string. The naming decision is
/// deliberately the only thing asserted - not the spelling of the possessive, which is Lee's to
/// change and would make this test an obstacle to changing it.
/// </para>
/// </remarks>
[Trait("Suite", "Slices")]
[Trait("Priority", "P0")]
[Trait("Requirement", "FR-4")]
[Trait("Requirement", "AR-27")]
[Trait("Assumption", "PRD-12-1")]
public sealed class ResourcePersonalSpaceNamingTests
{
    /// <summary>
    /// The resource key the adapter asks for is actually answered by the resource set.
    /// </summary>
    /// <remarks>
    /// Asserted through <c>ResourceNotFound</c> rather than by comparing the value to a spelling,
    /// because the spelling is the part that is allowed to change. This fails on a renamed key, a
    /// missing satellite, or a manifest name knocked out of step by a <c>ResourcesPath</c> - and
    /// it fails saying which.
    /// </remarks>
    [Fact]
    public void The_personal_space_name_key_resolves_to_a_real_resource()
    {
        var localised = Localizer()[RegistrationCopy.PersonalSpaceNameKey];

        Assert.False(
            localised.ResourceNotFound,
            $"RegistrationCopy.resx has no entry for '{RegistrationCopy.PersonalSpaceNameKey}', so " +
            "IStringLocalizer returned the key itself. string.Format leaves a template with no " +
            "placeholder unchanged, so every Space would be named after the key. Checked here " +
            "because no other test constructs the real adapter.");
    }

    /// <summary>
    /// The composed name carries the display name, and is not the resource key echoed back.
    /// </summary>
    [Fact]
    public void The_composed_name_carries_the_display_name()
    {
        var naming = new ResourcePersonalSpaceNaming(Localizer());

        const string displayName = "Ravi";

        var name = naming.NameFor(displayName);

        Assert.Contains(displayName, name, StringComparison.Ordinal);

        Assert.NotEqual(RegistrationCopy.PersonalSpaceNameKey, name);
    }

    /// <summary>
    /// The whole point of the choke point: changing the answer changes the resource entry, and
    /// this test does not stand in the way of changing it.
    /// </summary>
    /// <remarks>
    /// A display name with a possessive-defeating shape - one already ending in <c>s</c> - is used
    /// deliberately. It must still compose, because the template is a format string rather than a
    /// grammar engine, and a later story adding real possessive logic should find this test
    /// unopinionated about the result beyond the display name surviving into it.
    /// </remarks>
    [Theory]
    [InlineData("Ravi")]
    [InlineData("Jonas")]
    [InlineData("Nadia Okonkwo")]
    [InlineData("李")]
    public void Any_display_name_composes(string displayName)
    {
        var name = new ResourcePersonalSpaceNaming(Localizer()).NameFor(displayName);

        Assert.Contains(displayName, name, StringComparison.Ordinal);
    }

    /// <summary>
    /// An <see cref="IStringLocalizer{T}"/> over the real resource set, built the way the Host
    /// builds it.
    /// </summary>
    private static IStringLocalizer<RegistrationCopy> Localizer()
    {
        var services = new ServiceCollection();

        // AddLogging as well as AddLocalization: ResourceManagerStringLocalizerFactory takes an
        // ILoggerFactory, so AddLocalization alone builds a provider that throws on first
        // resolution rather than one that answers with the wrong string. Worth knowing, because
        // the failure is a DI message that says nothing about resources.
        services.AddLogging();
        services.AddLocalization();

        // Deliberately not disposed through a using: the localizer holds a reference to the
        // provider's ResourceManagerStringLocalizerFactory, and disposing the provider before the
        // caller reads a string would fail for a reason that has nothing to do with the assertion.
        // The provider is collected with the test.
        var provider = services.BuildServiceProvider();

        return provider.GetRequiredService<IStringLocalizer<RegistrationCopy>>();
    }
}
