using Microsoft.Playwright;
using Xunit;

namespace Yello.Tests.Client;

/// <summary>
/// What story 2.8's keyboard cases share: waiting until the Board's keyboard module is attached, resetting focus, naming
/// where focus is, and recording - in the page - every sentence the polite live regions say and whether each Space was
/// prevented. <see cref="KeyboardTests"/> and <see cref="TextSpacingTests"/> use it.
/// </summary>
/// <remarks>
/// <para>
/// <b>The module attaches after the Board's first render</b>, asynchronously. A key pressed before then is the browser's
/// alone, so every case waits for it first (<see cref="WaitForKeysAsync"/>): it dispatches a cancelable <c>ArrowUp</c> on the
/// Board's one tab stop - the first card, at the top of its column, where <c>↑</c> moves nothing - until the module prevents
/// it. A sleep would race the import; this does not.
/// </para>
/// <para>
/// <b>A repeated sentence is observed by a <c>MutationObserver</c></b> on every polite region, recording the region's text
/// each time it changes to something non-empty (<see cref="RecordAnnouncements"/>). A region whose text is set to the same
/// sentence again does not change, records nothing, and is the defect story 2.8 AC14 closes.
/// </para>
/// </remarks>
internal static class KeyboardHarness
{
    /// <summary>
    /// Installs the announcement recorder: <c>window.__said</c> gains each non-empty text a polite region changes to.
    /// </summary>
    public const string RecordAnnouncements =
        """
        () => {
            window.__said = [];
            for (const region of document.querySelectorAll('[aria-live="polite"]')) {
                new MutationObserver(() => {
                    const text = region.textContent.trim();
                    if (text) window.__said.push(text);
                }).observe(region, { childList: true, characterData: true, subtree: true });
            }
        }
        """;

    /// <summary>
    /// Installs the key recorder: <c>window.__space</c> and <c>window.__arrows</c> gain, for each Space or arrow <c>keydown</c>
    /// that reaches the window, whether it was prevented; <c>window.__escapes</c> counts each <c>Escape</c> that reaches the
    /// document.
    /// </summary>
    public const string RecordKeys =
        """
        () => {
            window.__space = [];
            window.__arrows = [];
            window.__escapes = 0;
            window.addEventListener('keydown', event => { if (event.key === ' ') window.__space.push(event.defaultPrevented); });
            window.addEventListener('keydown', event => { if (event.key.startsWith('Arrow')) window.__arrows.push(event.defaultPrevented); });
            document.addEventListener('keydown', event => { if (event.key === 'Escape') window.__escapes++; });
        }
        """;

    /// <summary>
    /// Where focus is, named: a card's title, <c>actions:{title}</c> for its actions control, <c>column:{Status}</c> for a
    /// column itself, <c>body</c>, or the element's tag and accessible text.
    /// </summary>
    private const string Describe =
        """
        () => {
            const a = document.activeElement;
            if (!a || a === document.body) return 'body';
            const titleOf = card => card.querySelector('.task-card__title').textContent.trim();
            if (a.matches('[data-task-id]')) return titleOf(a.closest('.task-card'));
            if (a.matches('.task-card__actions')) return 'actions:' + titleOf(a.closest('.task-card'));
            if (a.matches('section[data-status-id]')) return 'column:' + a.querySelector('h2').textContent.trim();
            return a.tagName + ':' + (a.getAttribute('aria-label') || a.textContent.trim());
        }
        """;

    /// <summary>
    /// Waits until the Board's keyboard module is attached (see the remarks).
    /// </summary>
    public static Task WaitForKeysAsync(IPage page) =>
        page.WaitForFunctionAsync(
            """
            () => {
                const stop = document.querySelector('.board__columns [tabindex="0"]');
                if (!stop) return false;
                stop.focus();
                return !stop.dispatchEvent(new KeyboardEvent('keydown', { key: 'ArrowUp', bubbles: true, cancelable: true }));
            }
            """,
            null,
            new PageWaitForFunctionOptions { Timeout = DragHarness.WaitMs, PollingInterval = 50 });

    /// <summary>
    /// Puts focus back at the top of the document, as a fresh Tab sequence starts (<c>BoardTests</c>' idiom).
    /// </summary>
    public static Task ResetFocusAsync(IPage page) =>
        page.EvaluateAsync("() => { document.activeElement?.blur(); document.body.tabIndex = -1; document.body.focus(); document.body.removeAttribute('tabindex'); }");

    /// <summary>
    /// Where focus is now (see <see cref="Describe"/>).
    /// </summary>
    public static Task<string> FocusedAsync(IPage page) => page.EvaluateAsync<string>(Describe);

    /// <summary>
    /// Waits until focus is <paramref name="expected"/>, polled; on the deadline, fails naming where focus is.
    /// </summary>
    public static async Task WaitForFocusAsync(IPage page, string expected)
    {
        try
        {
            await page.WaitForFunctionAsync($"expected => ({Describe})() === expected", expected, new PageWaitForFunctionOptions { Timeout = DragHarness.WaitMs, PollingInterval = 50 });
        }
        catch (TimeoutException)
        {
            Assert.Equal(expected, await FocusedAsync(page));
            throw;
        }
    }

    /// <summary>
    /// Presses <paramref name="key"/> and waits until focus is <paramref name="expected"/>.
    /// </summary>
    public static async Task PressAndFocusAsync(IPage page, string key, string expected)
    {
        await page.Keyboard.PressAsync(key);
        await WaitForFocusAsync(page, expected);
    }

    /// <summary>
    /// Focuses a card's title by its title, as a click or a returning panel would.
    /// </summary>
    public static async Task FocusCardAsync(IPage page, string title)
    {
        await DragHarness.CardNamed(page, title).Locator("[data-task-id]").FocusAsync(new LocatorFocusOptions { Timeout = DragHarness.WaitMs });
        await WaitForFocusAsync(page, title);
    }

    /// <summary>
    /// Every sentence the polite regions have said since <see cref="RecordAnnouncements"/> ran, in order.
    /// </summary>
    public static async Task<List<string>> SaidAsync(IPage page) => [.. await page.EvaluateAsync<string[]>("() => window.__said")];

    /// <summary>
    /// Waits until the polite regions have said <paramref name="sentence"/> at least <paramref name="times"/> times - alone, or with a sentence said in the same render.
    /// </summary>
    public static async Task WaitUntilSaidAsync(IPage page, string sentence, int times = 1)
    {
        try
        {
            await page.WaitForFunctionAsync(
                "([sentence, times]) => window.__said.filter(said => said.includes(sentence)).length >= times",
                new object[] { sentence, times },
                new PageWaitForFunctionOptions { Timeout = DragHarness.WaitMs, PollingInterval = 50 });
        }
        catch (TimeoutException)
        {
            Assert.Fail($"'{sentence}' was not said {times} time(s); said: [{string.Join(" | ", await SaidAsync(page))}].");
        }
    }

    /// <summary>
    /// Whether each Space that reached the window since <see cref="RecordKeys"/> ran was prevented.
    /// </summary>
    public static async Task<List<bool>> SpacesAsync(IPage page) => [.. await page.EvaluateAsync<bool[]>("() => window.__space")];

    /// <summary>
    /// Whether each arrow that reached the window since <see cref="RecordKeys"/> ran was prevented.
    /// </summary>
    public static async Task<List<bool>> ArrowsAsync(IPage page) => [.. await page.EvaluateAsync<bool[]>("() => window.__arrows")];

    /// <summary>
    /// How many <c>Escape</c>s reached the document since <see cref="RecordKeys"/> ran.
    /// </summary>
    public static Task<int> EscapesAsync(IPage page) => page.EvaluateAsync<int>("() => window.__escapes");

    /// <summary>
    /// The titles of the cards whose controls are in the tab order - every <c>tabindex="0"</c> in the row of columns, by its
    /// card (or <c>column:{Status}</c> for a column itself).
    /// </summary>
    public static async Task<List<string>> TabStopsAsync(IPage page) =>
        [.. await page.EvaluateAsync<string[]>(
            """
            () => [...document.querySelectorAll('.board__columns [tabindex="0"]')].map(element => {
                const card = element.closest('.task-card');
                return card ? card.querySelector('.task-card__title').textContent.trim() : 'column:' + element.querySelector('h2').textContent.trim();
            })
            """)];
}
