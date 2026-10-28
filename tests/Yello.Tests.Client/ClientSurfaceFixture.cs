using Microsoft.Playwright;
using Xunit;

namespace Yello.Tests.Client;

/// <summary>
/// One dev server and one browser, shared by every case in the browser suite.
/// </summary>
/// <remarks>
/// <para>
/// <b>Shared because starting either one is expensive and neither carries state between cases.</b>
/// The isolation this suite needs is per-<i>context</i>, not per-process: a fresh
/// <see cref="IBrowserContext"/> has its own cookie jar, its own storage and its own locale, which
/// is exactly the boundary the cases care about. Starting a browser per case would buy nothing and
/// cost seconds each.
/// </para>
/// <para>
/// <b>The browser is Chromium, pinned by the <c>Microsoft.Playwright</c> version.</b> Not
/// <c>Channel = "msedge"</c>, which drives whatever Edge the machine happens to have: that would
/// make the browser under test an unpinned dependency in a repository where every other version is
/// asserted by a gate, and a CI Linux agent has no Edge at all. The trade is one download per
/// machine, recorded in <c>Directory.Packages.props</c> and in the conventions document.
/// </para>
/// <para>
/// <b>A missing browser fails rather than skips.</b> <c>Yello.Tests.Slices</c> has a precedent for
/// skipping without a container runtime, and it is deliberately not followed here. This suite
/// exists because "the application starts" was asserted by nothing while 136 tests reported green;
/// a suite that silently reports zero cases when the browser is absent recreates that exact
/// condition, one level up. The failure message carries the one command that fixes it.
/// </para>
/// </remarks>
public sealed class ClientSurfaceFixture : IAsyncLifetime
{
    private ClientDevServer? _server;
    private IPlaywright? _playwright;
    private IBrowser? _browser;

    /// <summary>
    /// The address the client is being served on.
    /// </summary>
    public Uri BaseAddress =>
        _server?.BaseAddress
        ?? throw new InvalidOperationException(
            "The dev server is not running. Await InitializeAsync before reading this - xunit " +
            "does that through ICollectionFixture. If InitializeAsync threw, that exception is " +
            "the one to fix.");

    /// <summary>
    /// Opens a browser context with the given locale, and a page in it.
    /// </summary>
    /// <param name="locale">
    /// A BCP 47 tag. Playwright sets both <c>navigator.language</c> and the
    /// <c>Accept-Language</c> header from it, which is what makes the WebAssembly runtime boot in
    /// that culture - the same input a real browser supplies.
    /// </param>
    /// <returns>The context and its page. Disposing the context closes the page with it.</returns>
    public Task<(IBrowserContext Context, IPage Page)> OpenAsync(string locale) => OpenAsync(locale, timezoneId: null);

    /// <summary>
    /// Opens a browser context with the given locale and time zone, and a page in it. Added by the story 2.3
    /// ATDD run for AC7: a due date renders in the viewer's own zone, so a case sets the zone the browser
    /// reports.
    /// </summary>
    /// <param name="locale">A BCP 47 tag, as <see cref="OpenAsync(string)"/> takes it.</param>
    /// <param name="timezoneId">An IANA zone, for example <c>Asia/Tokyo</c>, or <c>null</c> for the machine's.</param>
    /// <returns>The context and its page. Disposing the context closes the page with it.</returns>
    public Task<(IBrowserContext Context, IPage Page)> OpenAsync(string locale, string? timezoneId) =>
        OpenAsync(locale, timezoneId, hasTouch: false);

    /// <summary>
    /// Opens a browser context with the given locale and time zone, as a touch device when asked, and a page in it.
    /// Added by story 2.7 (Q14 (a)): the long-press and pan cases drive a touch screen through the Chrome DevTools
    /// Protocol, and Chromium reports touch support to the page only for a context created with it.
    /// </summary>
    /// <param name="locale">A BCP 47 tag, as <see cref="OpenAsync(string)"/> takes it.</param>
    /// <param name="timezoneId">An IANA zone, or <c>null</c> for the machine's.</param>
    /// <param name="hasTouch">Whether the context is a touch device.</param>
    /// <returns>The context and its page. Disposing the context closes the page with it.</returns>
    public async Task<(IBrowserContext Context, IPage Page)> OpenAsync(string locale, string? timezoneId, bool hasTouch)
    {
        var browser = _browser
            ?? throw new InvalidOperationException(
                "The browser is not running. Await InitializeAsync before reading this.");

        var context = await browser.NewContextAsync(new BrowserNewContextOptions
        {
            Locale = locale,
            TimezoneId = timezoneId,
            HasTouch = hasTouch,
        }).ConfigureAwait(false);

        var page = await context.NewPageAsync().ConfigureAwait(false);

        return (context, page);
    }

    /// <summary>
    /// Everything the dev server has written, for a failure message that can explain itself.
    /// </summary>
    public string ServerOutput => _server?.Output ?? "(the dev server was never started)";

    /// <summary>
    /// Starts the dev server, then the browser.
    /// </summary>
    public async ValueTask InitializeAsync()
    {
        _server = await ClientDevServer.StartAsync().ConfigureAwait(false);

        _playwright = await Playwright.CreateAsync().ConfigureAwait(false);

        try
        {
            _browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
            {
                Headless = true,
            }).ConfigureAwait(false);
        }
        catch (PlaywrightException exception)
        {
            throw new InvalidOperationException(
                "Playwright could not launch Chromium. The browser is downloaded once per " +
                "machine and is not restored by NuGet, so a fresh clone or a cold CI agent " +
                "needs:" +
                Environment.NewLine +
                Environment.NewLine +
                "    pwsh tests/Yello.Tests.Client/bin/Debug/net10.0/playwright.ps1 install chromium" +
                Environment.NewLine +
                Environment.NewLine +
                "On a CI Linux agent add --with-deps, which installs the shared libraries " +
                "headless Chromium links against. This suite fails rather than skipping here " +
                "deliberately: it exists because \"the application starts\" was asserted by " +
                "nothing, and a silent skip would put it back to being asserted by nothing." +
                Environment.NewLine +
                Environment.NewLine +
                $"Playwright said: {exception.Message}",
                exception);
        }
    }

    /// <summary>
    /// Stops the browser, then the dev server.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (_browser is not null)
        {
            await _browser.DisposeAsync().ConfigureAwait(false);
        }

        _playwright?.Dispose();

        if (_server is not null)
        {
            await _server.DisposeAsync().ConfigureAwait(false);
        }
    }
}
