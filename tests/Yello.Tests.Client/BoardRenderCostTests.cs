using Microsoft.Playwright;
using Xunit;
using Yello.Tests.Shared;
using static Yello.Tests.Client.DragHarness;
using static Yello.Tests.Client.KeyboardHarness;

namespace Yello.Tests.Client;

/// <summary>
/// Story 2.9's client render cost at the NFR-8 scale, Nightly (AC14; test design R4, <c>test-design-architecture.md:459-471</c>;
/// NFR-3): one column with all 5,000 Tasks loaded by paging, then the time from a key to the focused card's update, measured
/// on the page's own clock, gated at the near-instant motion budget (<c>--motion-instant</c>, 90 ms); a pick-up, a carried
/// step and the JS heap are measured and recorded. Written red by the story 2.9 ATDD run.
/// </summary>
/// <remarks>
/// <para>
/// <b>If append-only cannot hold the budget, the story stops</b> and escalates an AD-29 amendment (a focus-identity-preserving
/// window) rather than introducing virtualisation (AC14) - record it as a blocker, never loosen this case.
/// </para>
/// <para>
/// <b>Timed, so alone</b>: <see cref="TouchTimingCollection"/> (<c>DisableParallelization</c>, the same
/// <see cref="ClientSurfaceFixture"/> - no new fixture). <b>Nightly</b> (<c>Tier=Nightly</c>, Q13 (a)): paging 5,000 cards in
/// takes 99 "Show more" activations, too slow for the PR stage.
/// </para>
/// </remarks>
[Trait("Suite", "Client")]
[Trait("Priority", "P1")]
[Trait("Tier", "Nightly")]
[Trait("Requirement", "AD-29")]
[Trait("Requirement", "NFR-3")]
[Collection(nameof(TouchTimingCollection))]
public sealed class BoardRenderCostTests(ClientSurfaceFixture surface)
{
    private const int Scale = 5_000;
    private const int PageSize = 50;
    private const int Steps = 21;
    private const double MotionInstantMilliseconds = 90;

    private static readonly Guid Northwind = Guid.NewGuid();
    private static readonly Guid Harbour = Guid.NewGuid();
    private static readonly Guid Atlas = Guid.NewGuid();

    /// <summary>
    /// AC14 - at the foot of 5,000 loaded cards, a keyboard step's time from <c>keydown</c> to the next card's
    /// <c>focusin</c> has a p95 within 90 ms over 21 steps; pick-up, a carried step and the heap are recorded.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Explicit = true)]
    public async Task A_keyboard_step_at_the_foot_of_5000_loaded_cards_lands_within_the_near_instant_budget()
    {
        var api = TwoSpaces(Northwind, Harbour);

        api.PageSize = PageSize;
        _ = Seed(api, Northwind, Atlas, ThreeStatuses, Many(Scale));

        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await api.InstallAsync(page);
            await OpenBoardAsync(page, surface.BaseAddress, Northwind, Atlas);
            await WaitForKeysAsync(page);

            var appends = new List<double>();

            for (var loaded = PageSize; loaded < Scale; loaded += PageSize)
            {
                var started = await page.EvaluateAsync<double>("() => performance.now()");

                await ColumnNamed(page, "Todo").GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { NameRegex = new System.Text.RegularExpressions.Regex("^Show more Tasks", System.Text.RegularExpressions.RegexOptions.None, TimeSpan.FromSeconds(1)) })
                    .ClickAsync(new LocatorClickOptions { Timeout = WaitMs });
                await page.WaitForFunctionAsync(
                    "count => [...document.querySelectorAll('main .board-column')][0].querySelectorAll('li.task-card').length === count",
                    loaded + PageSize,
                    new PageWaitForFunctionOptions { Timeout = WaitMs, PollingInterval = 50 });
                appends.Add(await page.EvaluateAsync<double>("started => performance.now() - started", started));
            }

            await page.EvaluateAsync(
                """
                () => {
                    window.__steps = [];
                    let pressed = 0;
                    document.addEventListener('keydown', event => { if (event.key === 'ArrowDown' || event.key === 'ArrowUp') pressed = performance.now(); }, true);
                    document.addEventListener('focusin', () => { if (pressed) { window.__steps.push(performance.now() - pressed); pressed = 0; } }, true);
                }
                """);

            await FocusCardAsync(page, "Card 4990");

            for (var step = 0; step < Steps; step++)
            {
                await PressAndFocusAsync(page, step % 2 == 0 ? "ArrowDown" : "ArrowUp", step % 2 == 0 ? "Card 4991" : "Card 4990");
            }

            var steps = await page.EvaluateAsync<double[]>("() => window.__steps");
            var pickUp = await TimeToClassAsync(page, " ", "task-card--carried");
            var carried = await page.EvaluateAsync<double>(
                "() => new Promise(resolve => { const t0 = performance.now(); new MutationObserver((_, observer) => { observer.disconnect(); resolve(performance.now() - t0); }).observe(document.querySelector('.board__columns'), { subtree: true, childList: true }); document.activeElement.dispatchEvent(new KeyboardEvent('keydown', { key: 'ArrowDown', bubbles: true, cancelable: true })); })");
            var heap = await page.EvaluateAsync<double>("() => performance.memory ? performance.memory.usedJSHeapSize / 1048576 : -1");

            await page.Keyboard.PressAsync("Escape");

            var p95 = Percentile.NearestRank(steps, 95);

            TestContext.Current.TestOutputHelper?.WriteLine(string.Create(
                System.Globalization.CultureInfo.InvariantCulture,
                $"5,000 cards loaded: append p95 {Percentile.NearestRank(appends, 95):F1} ms (max {appends.Max():F1}); keyboard step p95 {p95:F1} ms over {steps.Length}; pick-up {pickUp:F1} ms; carried step {carried:F1} ms; JS heap {heap:F1} MB. Local Chromium, page clock."));

            Assert.Equal(Steps, steps.Length);
            Assert.True(p95 <= MotionInstantMilliseconds, $"A keyboard step at 5,000 loaded cards took {p95:F1} ms at p95; the near-instant budget is {MotionInstantMilliseconds} ms (AC14).");
        }
    }

    /// <summary>
    /// The time on the page's clock from pressing <paramref name="key"/> on the focused card to an element gaining
    /// <paramref name="className"/>.
    /// </summary>
    private static async Task<double> TimeToClassAsync(IPage page, string key, string className)
    {
        await page.EvaluateAsync(
            """
            className => {
                window.__classAt = 0;
                window.__keyAt = 0;
                document.addEventListener('keydown', () => { if (!window.__keyAt) window.__keyAt = performance.now(); }, { capture: true, once: true });
                new MutationObserver((records, observer) => {
                    if (records.some(record => record.target.classList && record.target.classList.contains(className))) {
                        window.__classAt = performance.now();
                        observer.disconnect();
                    }
                }).observe(document.body, { subtree: true, attributes: true, attributeFilter: ['class'] });
            }
            """,
            className);

        await page.Keyboard.PressAsync(key);
        await page.WaitForFunctionAsync("() => window.__classAt > 0", null, new PageWaitForFunctionOptions { Timeout = WaitMs, PollingInterval = 50 });

        return await page.EvaluateAsync<double>("() => window.__classAt - window.__keyAt");
    }
}
