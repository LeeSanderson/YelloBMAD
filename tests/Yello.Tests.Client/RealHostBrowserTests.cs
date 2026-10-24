using System.Net;
using Microsoft.Playwright;
using Xunit;

namespace Yello.Tests.Client;

/// <summary>
/// Story 1.9 AC11 - the real client in Chromium against a real Host and a real database: the real
/// <c>SameSite=None; Secure</c> cookie survives the two origins, the real preflight admits the
/// anti-forgery header, a deep link to the other Account's Space renders the refusal page without
/// requesting it, and a real <c>401</c> reaches the client's session-end path.
/// <c>deferred-work.md:157-163, 318</c>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Activated by story 1.9 Task 9</b> on <see cref="RealHostCollection"/>. The scaffold's
/// <c>IRealHostWorld</c> contract became <see cref="RealHostFixture"/>.
/// </para>
/// <para>
/// <b>The Host's log, not a fake's request list, is the witness</b> that B's id was never requested.
/// </para>
/// </remarks>
/// <param name="world">The real Host, the dev server and the browser.</param>
[Trait("Suite", "Client")]
[Trait("Priority", "P0")]
[Trait("Requirement", "NFR-1")]
[Trait("Requirement", "AR-14")]
[Trait("Requirement", "UX-DR31")]
[Collection(nameof(RealHostCollection))]
public sealed class RealHostBrowserTests(RealHostFixture world)
{
    private const float Timeout = 30_000;

    private const string RoleChip = "nav.context-bar .role-chip";
    private const string SpaceName = "nav.context-bar .context-bar__space-name";
    private const string Heading = "#app h1";

    /// <summary>
    /// 1.9-E2E-006 - signing in through <c>SignIn.razor</c> against the real Host lands on A's Space
    /// with the bar and the Role chip drawn: the real cookie crossed the two origins, and the real
    /// preflight admitted the anti-forgery header.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "FR-2")]
    public async Task Signing_in_against_the_real_Host_lands_on_the_Space_with_the_bar_drawn()
    {
        RealHostFixture.SkipUnlessAnEngineIsConfigured();

        var (context, page) = await world.OpenSignedInAsync(world.A);

        await using (context)
        {
            await WaitForPathAsync(page, $"/spaces/{world.A.SpaceId:D}");
            await page.Locator(RoleChip).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

            Assert.Equal(world.A.SpaceName, (await page.Locator(SpaceName).TextContentAsync())?.Trim());
        }
    }

    /// <summary>
    /// 1.9-E2E-007 / AC2 - a deep link to B's Space renders the refusal page, and the Host received no
    /// request naming B's Space. Anchored on the Host having logged the switcher request the full load
    /// made, so the absence is read from a log that did record this navigation.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "UX-DR32")]
    public async Task A_deep_link_to_another_Accounts_Space_is_refused_without_reaching_the_Host()
    {
        RealHostFixture.SkipUnlessAnEngineIsConfigured();

        var (context, page) = await world.OpenSignedInAsync(world.A);

        await using (context)
        {
            await page.Locator(RoleChip).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

            var cursor = await world.Host.LogCursorAsync();
            var switcher = $"GET {world.Host.Address.GetLeftPart(UriPartial.Authority)}/api/v1/spaces ";

            await page.GotoAsync(new Uri(world.ClientAddress, $"spaces/{world.B.SpaceId:D}").ToString());
            await page.Locator(Heading, new PageLocatorOptions { HasText = "Not available." })
                .WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

            // The slice is read only after the page has gone quiet and a sentinel request sent after it
            // has its own line, so a request for B's Space still in flight cannot fall outside it.
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

            var sentinel = $"/api/sentinel-{Guid.NewGuid():N}";

            using (await world.Host.Client.GetAsync(new Uri(sentinel, UriKind.Relative), TestContext.Current.CancellationToken))
            {
                // Only the Host's line for it matters.
            }

            var log = await world.Host.WaitForLogAsync(
                cursor,
                slice => slice.Contains(switcher, StringComparison.Ordinal)
                    && Array.Exists(slice.Split('\n'), line => line.Contains("Request finished", StringComparison.Ordinal) && line.Contains(sentinel, StringComparison.Ordinal)));

            Assert.DoesNotContain(world.B.SpaceId.ToString("D"), log, StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <summary>
    /// 1.9-E2E-008 - an Account's Sessions ended on the server (sign-out from a second context, which
    /// stamps <c>SessionsValidAfter</c>): the browser's next navigation gets a real <c>401</c> and lands
    /// on <c>/sign-in</c> with the reason stated, the Space-scoped state purged.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "FR-2")]
    [Trait("Requirement", "NFR-2")]
    public async Task A_Session_ended_on_the_server_lands_the_client_on_sign_in_with_the_reason()
    {
        RealHostFixture.SkipUnlessAnEngineIsConfigured();

        // A fresh Account, because this case ends every Session it holds - with a second Space, so the
        // running client has somewhere listed to move to.
        var actor = await world.NewActorAsync();
        var second = await world.AddSecondSpaceAsync(actor);
        var (context, page) = await world.OpenSignedInAsync(actor);

        await using (context)
        {
            await page.Locator(RoleChip).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

            // The client lands on the first Space its switcher lists, which is either of the two; the
            // in-app move below is to the other one, so the running client has to ask the Host.
            var shown = new Uri(page.Url).AbsolutePath;
            var other = shown.EndsWith(second.ToString("D"), StringComparison.OrdinalIgnoreCase) ? actor.SpaceId : second;

            // The second context: the Account's own API Session signs out, which ends every Session the
            // Account holds - the browser's included (Lee's decision 1).
            Assert.Equal(HttpStatusCode.NoContent, (await actor.SignOutAndKeepTheOldCookieAsync()).StatusCode);

            var cursor = await world.Host.LogCursorAsync();

            // In-app, not a reload, and to the Space not shown: a reload boots a client that never held
            // the Session, which treats a 401 as "not signed in" rather than "your Session ended"; and a
            // navigation to the Space already shown asks the Host nothing. The running client is the one
            // whose session-end path is under test. A window marker proves no reload happened.
            await page.EvaluateAsync("() => { window.__inApp = true; }");
            await page.EvaluateAsync("id => Blazor.navigateTo('/spaces/' + id)", other.ToString("D"));
            await WaitForPathAsync(page, "/sign-in");

            Assert.True(await page.EvaluateAsync<bool>("() => window.__inApp === true"), "The navigation reloaded the page, so it was not the running client's session-end path.");

            // The 401 was the real Host's, for the Space this navigation asked for.
            var refused = other.ToString("D");

            await world.Host.WaitForLogAsync(
                cursor,
                slice => Array.Exists(slice.Split('\n'), line =>
                    line.Contains("Request finished", StringComparison.Ordinal)
                    && line.Contains(refused, StringComparison.OrdinalIgnoreCase)
                    && line.Contains(" - 401 ", StringComparison.Ordinal)));

            Assert.Contains("reason=session-ended", page.Url, StringComparison.Ordinal);
            Assert.Equal(0, await page.Locator(SpaceName).CountAsync());
            Assert.DoesNotContain(actor.SpaceName, await page.Locator("#app").InnerTextAsync(), StringComparison.Ordinal);
        }
    }

    private static async Task WaitForPathAsync(IPage page, string path)
    {
        try
        {
            await page.WaitForFunctionAsync(
                "path => location.pathname === path",
                path,
                new PageWaitForFunctionOptions { Timeout = Timeout });
        }
        catch (TimeoutException exception)
        {
            throw new InvalidOperationException(
                $"The page never reached {path}; it is at {page.Url}, showing: {await page.Locator("#app").InnerTextAsync()}",
                exception);
        }
    }
}
