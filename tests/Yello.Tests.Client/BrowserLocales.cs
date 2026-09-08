using Xunit;

namespace Yello.Tests.Client;

/// <summary>
/// The browser locales every startup case runs under, held as data.
/// </summary>
/// <remarks>
/// <para>
/// <b>This is deliberately NOT <c>SupportedCultures.All</c>, and the distinction is the whole
/// value of the list.</b> <c>SupportedCultures.All</c> has exactly one entry, <c>en</c>, because
/// there is one neutral resource set - so iterating it would run every case once, under the one
/// locale, and that is precisely the run that misses this class of defect.
/// <c>deferred-work.md</c> records the instruction in as many words: "run it <i>per supported
/// culture</i>, not once ... the class it belongs to is startup work conditioned on the browser's
/// culture, and that class is exactly what a single <c>en-GB</c> run misses."
/// </para>
/// <para>
/// What a browser <i>sends</i> and what Yello <i>serves</i> are different lists, and startup
/// reads the first one. The client asks the WebAssembly runtime for
/// <c>CultureInfo.CurrentCulture</c>, which the runtime takes from the browser - so the input
/// space is BCP 47 tags in general, not Yello's resource set. These four are chosen to cover the
/// resolution paths and the rendering rules that actually branch on it, and they are the four the
/// 2026-09-01 fix was verified under.
/// </para>
/// <para>
/// <b>Every one of them resolves to <c>en</c> today.</b> That is not a reason to collapse the
/// list: the cases assert the resolved value computed from <c>CultureSelection</c> rather than a
/// literal, so the day a second translation lands the expectations move on their own - and the
/// paths below stop being four spellings of the same answer.
/// </para>
/// </remarks>
internal static class BrowserLocales
{
    /// <summary>
    /// The locales, each covering a distinct path through startup.
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    ///   <item><description>
    ///     <c>en-GB</c> - <b>the case that actually broke.</b> It reaches Yello's resources by
    ///     CultureSelection's language step (<c>en-GB</c> is not an exact match; <c>en</c> is the
    ///     language) so the requested and served names differ, which is what the removed post-boot
    ///     assignment turned into a hard startup failure. It is also the majority locale for this
    ///     product's first users, which is why "works on my machine" and "works" were the same
    ///     sentence for a week.
    ///   </description></item>
    ///   <item><description>
    ///     <c>tr-TR</c> - Turkish, and the reason <c>base.css</c> has a casing exclusion list at
    ///     all. Uppercasing is lossy in Turkish, so <c>base.css</c> withholds
    ///     <c>text-transform: uppercase</c> under <c>:lang(tr)</c> - a rule that resolves against
    ///     the <c>lang</c> attribute this suite asserts, and which was inert while
    ///     <c>index.html</c> hard-coded <c>lang="en"</c>. Turkish is also the standard locale for
    ///     catching case-mapping bugs in ASCII identifiers, so a startup path that upper- or
    ///     lower-cases a culture name unsafely fails here first.
    ///   </description></item>
    ///   <item><description>
    ///     <c>ja-JP</c> - a case-less script, and a different ICU shard. The runtime loads one
    ///     shard chosen for the boot culture, and the client's build output carries three
    ///     (<c>icudt_EFIGS</c>, <c>icudt_CJK</c>, <c>icudt_no_CJK</c>); this is the locale that
    ///     exercises a shard the Latin cases never touch. <c>base.css</c> also withholds
    ///     letter-spacing from case-less scripts, where it severs connected joins.
    ///   </description></item>
    ///   <item><description>
    ///     <c>de-AT</c> - a region whose language Yello does not serve, so resolution falls all
    ///     the way through to the default. This is the path that changes behaviour the moment a
    ///     <c>de</c> translation exists, which makes it the case most likely to catch the trap
    ///     being re-armed by the story that adds one.
    ///   </description></item>
    /// </list>
    /// </remarks>
    public static TheoryData<string> All => ["en-GB", "tr-TR", "ja-JP", "de-AT"];
}
