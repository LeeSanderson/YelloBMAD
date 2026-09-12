using System.Globalization;
using System.Reflection;
using System.Resources;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Xunit;
using Yello.Application.Accounts.RegisterAccount;
using Yello.Client.Localisation;
using Yello.Client.Pages;
using Yello.Contracts.Localisation;

// Mono.Cecil.Cil has a DocumentLanguage of its own - it describes the source language of a
// debugging document - so the client's type is aliased rather than the production type renamed
// for a test's convenience.
using DocumentLanguage = Yello.Client.Localisation.DocumentLanguage;

namespace Yello.Tests.Architecture;

/// <summary>
/// The gates that keep story 1.3's localisation real rather than nominal.
/// </summary>
/// <remarks>
/// <para>
/// <b>The copy gate story 1.2 shipped requires resources; nothing required them to WORK.</b>
/// <c>No_user_visible_string_literal_appears_in_a_component</c> fails the build on a literal in
/// markup, so every string is externalised - and every one of them could still resolve to
/// nothing, in a document whose language is hard-coded, keyed off codes the server never sends.
/// These are the assertions that close that gap.
/// </para>
/// </remarks>
[Trait("Suite", "Architecture")]
[Trait("Priority", "P0")]
[Trait("Requirement", "UX-DR42")]
[Trait("Requirement", "NFR-9")]
public sealed class LocalisationGateTests
{
    /// <summary>
    /// Something in the client calls the document-language interop, so the mechanism
    /// <c>base.css</c>'s locale exclusions depend on is wired at all. UX-DR42.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Renamed at code review on 2026-09-10, because the old name claimed the half this does
    /// not check.</b> It was called
    /// <c>The_document_language_is_set_from_the_active_culture</c>, and nothing in it touches the
    /// culture. What it asserts is that some method in <c>Yello.Client</c> contains a call
    /// instruction to <see cref="DocumentLanguage.ApplyAsync"/>. It does not assert the argument,
    /// that the call is reached, or that it runs before <c>host.RunAsync()</c> -
    /// <c>ApplyAsync(js, "en")</c> with a hard-coded literal passes, and so does the call moved
    /// after the runtime has booted, which is the exact shape of the 2026-09-01 incident recorded
    /// in <c>Program.cs</c>. The name now says what it does; the story's Dev Agent Record and
    /// <c>deferred-work.md</c> were corrected to match.
    /// </para>
    /// <para>
    /// <b>This is the gate <c>deferred-work.md:32</c> asked for by name</b>, and it is worth being
    /// exact about what that entry got in return. The entry records that
    /// <c>&lt;html lang="en"&gt;</c> was hard-coded, which made <c>base.css:141-172</c> - three
    /// rules withholding <c>text-transform</c> from Turkish, Azeri and Greek and letter-spacing
    /// from 24 case-less scripts - <i>inert</i>, because every one of them is scoped by
    /// <c>:lang()</c>. This gate stops the wiring being deleted. It does <b>not</b> make the
    /// exclusions fire: <see cref="SupportedCultures"/> holds one entry, so every browser resolves
    /// to <c>en</c> and the attribute is written with the value <c>index.html</c> already
    /// hard-codes. The inertness is unchanged, and
    /// <see cref="The_supported_culture_list_has_exactly_one_entry"/> below is what says so out
    /// loud.
    /// </para>
    /// <para>
    /// <b>Read from compiled IL, not from source text.</b> A source scan for
    /// <c>document.documentElement</c> would be satisfied by the string appearing in a comment,
    /// in a doc remark, or in a constant nothing calls - and this class's own remarks mention it,
    /// so a naive scan would find itself.
    /// </para>
    /// <para>
    /// Validated by planting: commenting out the call in <c>Program.cs</c> fails this by name,
    /// and the result is in the story's Dev Agent Record.
    /// </para>
    /// </remarks>
    [Fact]
    public void The_client_calls_the_document_language_interop()
    {
        // The literals are asserted too. Without this, deleting the interop call and leaving the
        // constants would fail the IL check for the right reason, while CHANGING the constants -
        // to an attribute `:lang()` does not read - would pass it for the wrong one.
        Assert.Equal("document.documentElement.setAttribute", DocumentLanguage.SetAttributeFunction);
        Assert.Equal("lang", DocumentLanguage.LanguageAttribute);

        var clientAssembly = ProductionAssemblies.All
            .Single(assembly => assembly.GetName().Name == "Yello.Client");

        using var module = ModuleDefinition.ReadModule(clientAssembly.Location);

        var callers = module.GetTypes()
            .SelectMany(type => type.Methods)
            .Where(method => method.HasBody)
            .Where(method => method.Body.Instructions.Any(IsApplyCall))
            .Select(method => $"{method.DeclaringType.FullName}.{method.Name}")
            .ToList();

        Assert.True(
            callers.Count > 0,
            $"Nothing in Yello.Client calls {nameof(DocumentLanguage)}.{nameof(DocumentLanguage.ApplyAsync)}, " +
            "so the document keeps index.html's hard-coded `lang` and base.css's 26-locale " +
            "casing exclusions are inert - Turkish content would be uppercased lossily, and no " +
            "other gate in this suite would notice. See deferred-work.md's entry on this, which " +
            "named the first story to introduce a culture provider as its owner.");
    }

    /// <summary>
    /// Every culture Yello claims to support has resources behind it.
    /// </summary>
    /// <remarks>
    /// <b>A locale on the list with no translation is worse than an absent one.</b> It tells the
    /// client to render, and <c>&lt;html lang&gt;</c> to claim, a language the strings are not
    /// written in - so a screen reader pronounces English with German phonetics, and
    /// <c>base.css</c>'s casing exclusions start firing for content that is still English. Adding
    /// a translation means adding both halves, and this is what says so.
    /// </remarks>
    [Fact]
    public void Every_supported_culture_has_resources_behind_it()
    {
        var assembly = typeof(ClientCopy).Assembly;
        var manager = new ResourceManager(ClientCopy.ResourceName, assembly);

        var problems = new List<string>();

        // The neutral set must exist at all. Without this the loop below passes vacuously on a
        // one-entry list whose single entry is the default.
        var neutral = manager.GetResourceSet(CultureInfo.InvariantCulture, createIfNotExists: true, tryParents: false);

        if (neutral is null)
        {
            problems.Add(
                $"No neutral resource set was found for '{ClientCopy.ResourceName}'. Every string " +
                "in the interface resolves through it, so an absent one renders the whole product " +
                "as its own resource keys.");
        }

        problems.AddRange(
            from culture in SupportedCultures.All
            where !culture.Equals(SupportedCultures.Default, StringComparison.OrdinalIgnoreCase)
            let set = TryGetResourceSet(manager, culture)
            where set is null
            select $"SupportedCultures lists '{culture}', but no resource set exists for it. It " +
                   "would fall back to English while the interface claimed to be in that " +
                   "language.");

        Assert.True(problems.Count == 0, string.Join(Environment.NewLine, problems));
    }

    /// <summary>
    /// There is exactly one supported culture, so every locale still resolves to <c>en</c> and
    /// <c>base.css</c>'s casing exclusions remain unreachable.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>This is the assertion <c>deferred-work.md</c> proposed for itself, written at code
    /// review on 2026-09-10 - and writing it is what reopened the entry above it.</b> The ledger
    /// recorded the hard-coded <c>&lt;html lang&gt;</c> entry as CLOSED by this story, on the
    /// grounds that the mechanism now exists and is gated. The mechanism does exist. The
    /// <i>inertness</i> the entry was actually about does not change:
    /// <see cref="SupportedCultures.All"/> holds one entry, <c>CultureSelection.Resolve</c>
    /// therefore returns <c>en</c> for every browser, and the client writes <c>lang="en"</c> -
    /// byte-identical to what <c>index.html</c> hard-codes. Not one of the 26 locale exclusions
    /// can fire for any visitor.
    /// </para>
    /// <para>
    /// The entry's own original finding predicted this outcome in as many words: "wiring
    /// <c>lang</c> to a culture that is always <c>en</c> would be a no-op that looks like a fix".
    /// It was closed anyway. So this test exists to make the state legible rather than arguable -
    /// it passes while the product genuinely has one language, and fails the moment a second
    /// arrives, which is the moment the exclusions become real and someone has to check they
    /// work in a browser.
    /// </para>
    /// <para>
    /// <b>Failing on the day someone adds German is the intent, not a side effect.</b> Same shape
    /// as AC9's "at most one pill" from story 1.2: honest today, and it fails loudly on the one
    /// day a person needs to read it. Whoever deletes this test should be doing so as part of
    /// shipping that second translation, and should read the trap in the message first.
    /// </para>
    /// </remarks>
    [Fact]
    public void The_supported_culture_list_has_exactly_one_entry()
    {
        Assert.Equal("en", SupportedCultures.Default);

        // Stated as an assertion rather than a comment so the reason travels with the failure.
        Assert.True(
            SupportedCultures.All.Count == 1,
            "SupportedCultures now lists more than one culture, which is the day three things stop " +
            "being theoretical. (1) base.css's :lang() exclusions - no text-transform for tr/az/el, " +
            "no letter-spacing for 24 case-less scripts - finally become reachable, and NOTHING has " +
            "ever verified them in a rendered browser; the client suite can do that now. (2) " +
            "CultureSelection.Resolve compares TwoLetterISOLanguageName when it walks to a parent, " +
            "so a script-qualified supported culture (zh-Hans) will not match the way a reader " +
            "expects, and the Host and the client can then disagree about what a browser gets - an " +
            "English interface around a Space named in another language. (3) deferred-work.md's " +
            "entry on the 2026-09-01 ICU incident applies directly: the supported way to change " +
            "culture is blazor.start({ applicationCulture }) BEFORE the runtime boots, never a " +
            "post-boot assignment. Read that entry before deleting this test.");
    }

    /// <summary>
    /// Every failure code the server can return has a message the client can render, and belongs
    /// to a field the client knows about.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>This is the only place the two halves can be compared.</b> <c>Yello.Client</c> cannot
    /// reference <c>Yello.Application</c> - the ring table gives the client only Contracts and
    /// Merge - so the codes are declared on the server and consumed on the client with nothing
    /// linking them. The architecture suite is the one project that references both.
    /// </para>
    /// <para>
    /// Two things drift, and both are silent. A renamed code renders as the raw code text in the
    /// interface, which looks like a bug in the copy rather than in the contract. A code whose
    /// prefix matches no field detaches its message from the control it is about, leaving it only
    /// in the summary region - degraded rather than broken, and correspondingly easy to miss.
    /// </para>
    /// </remarks>
    [Fact]
    public void Every_registration_failure_code_has_a_message_and_a_field()
    {
        var codes = typeof(RegisterAccountFailure)
            .GetProperties(BindingFlags.Public | BindingFlags.Static)
            .Where(property => property.PropertyType == typeof(string))
            .Select(property => property.GetValue(null) as string)
            .Where(code => !string.IsNullOrEmpty(code))
            .Select(code => code!)
            .ToList();

        // The reflection above has to have found something, or every assertion below is an
        // assertion about an empty list.
        Assert.NotEmpty(codes);

        var manager = new ResourceManager(ClientCopy.ResourceName, typeof(ClientCopy).Assembly);
        var problems = new List<string>();

        problems.AddRange(
            from code in codes
            where manager.GetString(code, CultureInfo.InvariantCulture) is null
            select $"The server can return the failure code '{code}', and ClientCopy.resx has no " +
                   "entry for it - so the interface would render the code itself.");

        problems.AddRange(
            from code in codes
            where !RegistrationFields.All.Any(field => code.StartsWith(field, StringComparison.Ordinal))
            select $"The failure code '{code}' begins with none of the field prefixes in " +
                   "RegistrationFields, so its message would never appear under the control it " +
                   "is about.");

        Assert.True(problems.Count == 0, string.Join(Environment.NewLine, problems));
    }

    private static bool IsApplyCall(Instruction instruction) =>
        instruction.Operand is MethodReference called
        && called.Name.Equals(nameof(DocumentLanguage.ApplyAsync), StringComparison.Ordinal)
        && called.DeclaringType.FullName.Equals(
            typeof(DocumentLanguage).FullName,
            StringComparison.Ordinal);

    private static ResourceSet? TryGetResourceSet(ResourceManager manager, string culture)
    {
        try
        {
            return manager.GetResourceSet(
                CultureInfo.GetCultureInfo(culture), createIfNotExists: true, tryParents: false);
        }
        catch (MissingManifestResourceException)
        {
            return null;
        }
    }
}
