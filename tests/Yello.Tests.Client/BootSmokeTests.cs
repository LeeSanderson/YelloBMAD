using System.Collections.Concurrent;
using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.Playwright;
using Xunit;
using Yello.Contracts.Localisation;

namespace Yello.Tests.Client;

/// <summary>
/// The application starts, in a browser, in every locale that reaches it.
/// </summary>
/// <remarks>
/// <para>
/// <b>The gap this closes, stated plainly.</b> Story 1.3 shipped Yello's first rendered surface
/// and closed with 136 tests passing. The client did not start - in any browser, in any locale.
/// The registration surface was covered by 82 architecture gates and 54 slice tests: the markup was
/// parsed, the copy was scanned for literals, the tokens were checked for contrast, the compiled IL
/// was read for the document-language call. Every one of them read a <i>file</i>. Nothing loaded the
/// page. So a composition root that threw before the first render passed all 136, and what the user
/// saw was "Loading Yello..." behind a banner reading "An unhandled error has occurred."
/// </para>
/// <para>
/// <b>Why the assertions are shaped the way they are.</b> The specific 2026-09-01 cause - a
/// post-boot culture assignment against a sharded ICU dataset - is fixed and gated in three other
/// places. These cases deliberately assert the <i>effect</i> instead: the page renders and shows no
/// error. That is the lesson story 1.3's Plant D taught when a gate asserting an entity's presence
/// was rewritten to assert a configuration's effect. An assertion about the mechanism goes stale
/// with the mechanism; "the application starts" does not.
/// </para>
/// <para>
/// <b>Per locale, not once.</b> See <see cref="BrowserLocales"/> - a single <c>en-GB</c> run is
/// precisely the run that misses this class of defect.
/// </para>
/// </remarks>
[Trait("Suite", "Client")]
[Trait("Priority", "P0")]
[Trait("Requirement", "NFR-9")]
[Collection(nameof(ClientSurfaceCollection))]
public sealed partial class BootSmokeTests(ClientSurfaceFixture surface)
{
    /// <summary>
    /// How long to give the WebAssembly runtime to boot and render.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A deadline for reporting a hang, not a performance budget - NFR-5 and NFR-8 are measured
    /// elsewhere, by harnesses built to measure. A development build is unbundled and not
    /// ahead-of-time compiled, and the first page load of a run pays for the runtime download.
    /// </para>
    /// <para>
    /// <b>30 seconds is chosen against measurement, and against the cost of being wrong.</b> The
    /// whole suite - browser start, dev server start and eight page loads - runs in about 15
    /// seconds on a warm developer machine, so a single load has roughly twenty times the
    /// headroom it needs. The number matters in the other direction too: when the client genuinely
    /// cannot start, every case waits this out, so the timeout multiplied by the case count is how
    /// long a broken build takes to say so. It was 60 seconds first, and the planted-violation run
    /// of 2026-09-08 spent about eight minutes per plant discovering what it already knew.
    /// </para>
    /// </remarks>
    private const int RenderTimeoutMilliseconds = 30_000;

    /// <summary>
    /// The pre-boot placeholder <c>index.html</c> serves before the runtime exists. Its presence
    /// after boot means the root component never replaced it.
    /// </summary>
    /// <remarks>
    /// Written in pieces so this file does not itself contain the literal it is looking for -
    /// the same technique <c>TestingConventionTests</c> uses for the SQL Server image, and for
    /// the same reason: a complete literal here is indistinguishable from the thing being
    /// detected if either ever gets scanned.
    /// </remarks>
    private static readonly string PreBootPlaceholder = string.Concat("Loading", " Yello");

    [Theory]
    [MemberData(nameof(BrowserLocales.All), MemberType = typeof(BrowserLocales))]
    public async Task The_application_starts_and_renders_the_routed_page(string locale)
    {
        var (context, page) = await surface.OpenAsync(locale);
        var pageErrors = new ConcurrentQueue<string>();

        await using (context)
        {
            page.PageError += (_, error) => pageErrors.Enqueue(error);

            await page.GotoAsync(surface.BaseAddress.ToString());

            // FocusOnNavigate targets `h1`, so the routed page's heading is the element that
            // proves routing resolved and the component rendered - not merely that the runtime
            // downloaded.
            var heading = page.Locator("#app h1");

            try
            {
                await heading.WaitForAsync(new LocatorWaitForOptions
                {
                    Timeout = RenderTimeoutMilliseconds,
                    State = WaitForSelectorState.Attached,
                });
            }
            catch (TimeoutException exception)
            {
                Assert.Fail(await DiagnoseAsync(page, pageErrors, locale, exception));
            }

            var headingText = await heading.InnerTextAsync();

            Assert.False(
                string.IsNullOrWhiteSpace(headingText),
                $"The routed page's <h1> rendered empty under locale '{locale}'. The component " +
                "resolved but its copy did not, which is what a missing or unreachable resource " +
                "set looks like from the browser." +
                Environment.NewLine +
                await DiagnoseAsync(page, pageErrors, locale, exception: null));

            var appText = await page.Locator("#app").InnerTextAsync();

            Assert.DoesNotContain(PreBootPlaceholder, appText, StringComparison.Ordinal);

            // The framework unhides this element when an unhandled exception reaches it. Asserting
            // that it is not VISIBLE rather than that it still carries `display:none` asserts what
            // the user experiences: the inline style is the framework's own precondition for
            // unhiding, so reading it back would test the mechanism instead of the outcome.
            var errorBanner = page.Locator("#blazor-error-ui");

            Assert.False(
                await errorBanner.IsVisibleAsync(),
                $"The client rendered under locale '{locale}' but the unhandled-error banner is " +
                "showing, so something threw after the first paint." +
                Environment.NewLine +
                await DiagnoseAsync(page, pageErrors, locale, exception: null));
        }
    }

    /// <summary>
    /// The document's language is the culture the client actually resolved, in every locale.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The expected value is computed, never written down.</b> It comes from
    /// <c>CultureSelection.Resolve</c> against <c>SupportedCultures</c> - the one list the client
    /// and the Host both read. Today every locale below resolves to <c>en</c>, so a literal
    /// <c>"en"</c> would pass and would go on passing for the wrong reason; the day a
    /// <c>de</c> translation lands, <c>de-AT</c> must resolve to <c>de</c> and this case follows
    /// without being edited.
    /// </para>
    /// <para>
    /// <b>What it protects.</b> <c>base.css</c> keys 26 locale exclusions on <c>:lang()</c>, which
    /// resolves against this attribute and nothing else - including the rule that withholds lossy
    /// uppercasing from Turkish. While <c>index.html</c> hard-coded <c>lang="en"</c> every one of
    /// them was inert. An IL gate already asserts the call exists; this asserts the value that
    /// reaches the DOM, which is the half a bytecode gate cannot see.
    /// </para>
    /// <para>
    /// <b>The pre-boot attribute is rewritten before the page is served, and without that this
    /// case asserts nothing.</b> <c>index.html</c> hard-codes <c>lang="en"</c>; every locale
    /// resolves to <c>en</c> while there is one neutral resource set; so <c>"en" == "en"</c> holds
    /// whether the client wrote the attribute or never ran at all. That is not a worry, it is a
    /// measured fact: planted violation B on 2026-09-08 changed the attribute
    /// <c>DocumentLanguage</c> writes from <c>lang</c> to <c>data-lang</c> - disabling the feature
    /// completely - and all eight cases passed. Serving <c>und</c> in place of the static value
    /// makes the assertion strict, because <c>en</c> can then only have come from the client.
    /// </para>
    /// </remarks>
    [Theory]
    [Trait("Requirement", "AD-23")]
    [MemberData(nameof(BrowserLocales.All), MemberType = typeof(BrowserLocales))]
    public async Task The_document_language_is_the_culture_the_client_resolved(string locale)
    {
        var expected = CultureSelection.Resolve(
            CultureInfo.GetCultureInfo(locale),
            SupportedCultures.All,
            SupportedCultures.Default);

        var (context, page) = await surface.OpenAsync(locale);

        // Recorded from the route handler, which runs on Playwright's own task. A queue rather
        // than a captured local because the write and the read are on different threads.
        var rewrites = new ConcurrentQueue<bool>();

        await using (context)
        {
            await page.RouteAsync(surface.BaseAddress.ToString(), async route =>
            {
                var response = await route.FetchAsync();
                var body = await response.TextAsync();
                var rewritten = PreBootLanguageAttribute.Replace(body, "<html lang=\"und\"", 1);

                rewrites.Enqueue(!string.Equals(rewritten, body, StringComparison.Ordinal));

                await route.FulfillAsync(new RouteFulfillOptions
                {
                    Response = response,
                    Body = rewritten,
                });
            });

            await page.GotoAsync(surface.BaseAddress.ToString());

            // Wait for the render before reading the attribute: DocumentLanguage.ApplyAsync is
            // awaited before RunAsync, so a rendered heading means the attribute has been written.
            await page.Locator("#app h1").WaitForAsync(new LocatorWaitForOptions
            {
                Timeout = RenderTimeoutMilliseconds,
                State = WaitForSelectorState.Attached,
            });

            // Asserted before the attribute itself, because a rewrite that silently matched
            // nothing would put this case straight back to comparing "en" with "en". If
            // index.html's <html> tag is ever reformatted past this pattern, the failure has to
            // be loud - an inert check that reports green is the defect class this whole suite
            // exists to catch.
            Assert.True(
                rewrites.TryDequeue(out var served) && served,
                "The served document's pre-boot lang attribute was not rewritten, so this case " +
                "cannot distinguish an attribute the client set from the one index.html ships. " +
                "Either the route did not intercept the document, or index.html's <html> tag no " +
                "longer matches the pattern this case rewrites.");

            var actual = await page.Locator("html").GetAttributeAsync("lang");

            Assert.Equal(expected, actual, StringComparer.Ordinal);
        }
    }

    /// <summary>
    /// The <c>lang</c> attribute on the opening <c>html</c> tag of the served document.
    /// </summary>
    [GeneratedRegex("<html\\s+lang=\"[^\"]*\"", RegexOptions.IgnoreCase, matchTimeoutMilliseconds: 1000)]
    private static partial Regex PreBootLanguageAttribute { get; }

    /// <summary>
    /// Builds the failure message this suite exists to produce: what the page held, whether the
    /// error banner was showing, what the browser reported, and what the dev server said.
    /// </summary>
    /// <remarks>
    /// A bare timeout on a selector says "the element never appeared", which is true of a
    /// composition root that threw, a routing table that resolved nothing, and a dev server that
    /// served a blank page - three very different mornings. The 2026-09-01 defect presented as a
    /// page holding the pre-boot placeholder under a banner whose text named nothing; a message
    /// that reports both is the difference between a diagnosis and a starting point.
    /// </remarks>
    private static async Task<string> DiagnoseAsync(
        IPage page,
        ConcurrentQueue<string> pageErrors,
        string locale,
        Exception? exception)
    {
        var appText = await SafeAsync(async () => await page.Locator("#app").InnerTextAsync());
        var bannerVisible = await SafeAsync(async () =>
            (await page.Locator("#blazor-error-ui").IsVisibleAsync())
                .ToString(CultureInfo.InvariantCulture));
        var documentLanguage = await SafeAsync(async () => await page.Locator("html").GetAttributeAsync("lang"));

        var stillOnPlaceholder = appText?.Contains(PreBootPlaceholder, StringComparison.Ordinal) == true;

        return
            $"The client did not render under locale '{locale}'." +
            Environment.NewLine +
            Environment.NewLine +
            $"  #app text:            {Describe(appText)}" + Environment.NewLine +
            $"  error banner visible: {Describe(bannerVisible)}" + Environment.NewLine +
            $"  <html lang>:          {Describe(documentLanguage)}" + Environment.NewLine +
            $"  page errors:          {(pageErrors.IsEmpty ? "(none)" : string.Join(" | ", pageErrors))}" +
            Environment.NewLine +
            (exception is null ? string.Empty : $"  waited:               {exception.Message}{Environment.NewLine}") +
            Environment.NewLine +
            (stillOnPlaceholder
                ? "The page is still holding the pre-boot placeholder, which means the root " +
                  "component never replaced it: the failure is in the client's composition root " +
                  "and happened before the first render. That is the exact shape of the " +
                  "2026-09-01 defect - see the change log entry in story 1.3 and the entries in " +
                  "deferred-work.md that this suite was built to close."
                : "The placeholder was replaced, so the runtime booted and the failure is after " +
                  "startup - routing, the layout, or the component itself.");
    }

    /// <summary>
    /// Reads a value for a diagnostic message, turning a failure to read it into text rather than
    /// into a second exception that replaces the first.
    /// </summary>
    private static async Task<string?> SafeAsync(Func<Task<string?>> read)
    {
        try
        {
            return await read();
        }
        catch (PlaywrightException exception)
        {
            return $"(unreadable: {exception.Message})";
        }
        catch (TimeoutException exception)
        {
            return $"(unreadable: {exception.Message})";
        }
    }

    private static string Describe(string? value) =>
        string.IsNullOrWhiteSpace(value) ? "(empty)" : value.ReplaceLineEndings(" ");
}
