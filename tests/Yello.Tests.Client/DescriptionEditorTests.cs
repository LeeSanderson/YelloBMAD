using System.Text.RegularExpressions;
using Microsoft.Playwright;
using Xunit;

namespace Yello.Tests.Client;

/// <summary>
/// The description region of the Task detail panel in a real browser: an editor on the page ground under its own heading,
/// with no save, merge, lock or saving text; local edits render at once and go to the server in batches, never one request
/// per keystroke; refused changes are withdrawn; a failed batch is kept for the next; <c>Esc</c> leaves the editor, then
/// closes the panel. A Viewer gets the same region and heading with the text rendered and no editor in the DOM.
/// </summary>
/// <remarks>
/// <para>
/// <b>The API is <see cref="FakeYelloApi"/></b> with the description partial (<c>FakeYelloApi.TaskDescription.cs</c>), which
/// the develop phase routes and backs with a replica of the real strategy. Every case is red until then.
/// </para>
/// <para>
/// <b>The locators and copy are the story's</b>: the region is <c>section.task-detail__description</c> labelled by its
/// heading "Description"; the editor is <c>textarea.description-editor</c>; an empty description reads "No description.".
/// A different choice corrects the constant here and is recorded.
/// </para>
/// </remarks>
[Trait("Suite", "Client")]
[Trait("Priority", "P0")]
[Trait("Requirement", "UX-DR10")]
[Trait("Requirement", "FR-31")]
public sealed class DescriptionEditorTests(ClientSurfaceFixture surface)
{
    private const float Timeout = 30_000;
    private const string EnglishLocale = "en-GB";
    private const string RoleChip = "nav.context-bar .role-chip";
    private const string Card = ".task-card";
    private const string Dialog = "[role='dialog']";
    private const string Region = "section.task-detail__description";
    private const string Editor = "textarea.description-editor";
    private const string HeadingText = "Description";
    private const string EmptyCopy = "No description.";

    private static readonly string[] Banned = ["Save", "Saving", "Saved", "Merge", "Conflict", "Locked"];

    private static readonly string[] StatusNames = ["Todo", "Doing", "Done"];

    private static readonly Guid Northwind = Guid.NewGuid();
    private static readonly Guid Atlas = Guid.NewGuid();

    /// <summary>
    /// The region sits below the attributes inside the panel, a <c>section</c> labelled by its own heading "Description"; the
    /// editor inside it is a textarea on the page ground (not the card ground) with spellcheck left on.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task The_editor_sits_in_a_labelled_region_below_the_attributes_on_the_page_ground()
    {
        var api = OwnerSpace("owner");
        var taskId = Seed(api, "Draft the brief");
        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenPanelAsync(api, page, taskId);

            var region = page.Locator($"{Dialog} {Region}");

            Assert.Equal(1, await region.CountAsync());
            Assert.Equal(
                HeadingText,
                await region.EvaluateAsync<string>("r => { const h = document.getElementById(r.getAttribute('aria-labelledby')); return h && r.contains(h) ? h.textContent.trim() : ''; }"));
            Assert.True(
                await page.Locator(Dialog).EvaluateAsync<bool>("(d, sel) => { const r = d.querySelector(sel); const t = d.querySelector('textarea[data-single-line]'); return !!r && !!t && (t.compareDocumentPosition(r) & Node.DOCUMENT_POSITION_FOLLOWING) !== 0; }", Region),
                "The description region is not below the attributes.");

            var editor = region.Locator(Editor);

            Assert.Equal(1, await editor.CountAsync());
            Assert.NotEqual("false", await editor.GetAttributeAsync("spellcheck"));
            Assert.Equal(await TokenColourAsync(page, "--surface-page"), await editor.EvaluateAsync<string>("e => getComputedStyle(e).backgroundColor"));
            Assert.NotEqual(await TokenColourAsync(page, "--surface-card"), await editor.EvaluateAsync<string>("e => getComputedStyle(e).backgroundColor"));
        }
    }

    /// <summary>
    /// A local edit renders at once, while its batch is still held; nothing on the page reads or is named "Save", "Saving",
    /// "Saved", "Merge", "Conflict" or "Locked", before or after the batch is answered.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "AD-11")]
    [Trait("Requirement", "UX-DR26")]
    public async Task A_local_edit_renders_at_once_and_nothing_saves_merges_or_locks()
    {
        var api = OwnerSpace("owner");
        var taskId = Seed(api, "Draft the brief");
        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenPanelAsync(api, page, taskId);

            var held = api.HoldDescriptionBatchAsync();
            var editor = page.Locator($"{Dialog} {Editor}");

            await editor.PressSequentiallyAsync("typed locally");
            await held.WaitAsync(TimeSpan.FromMilliseconds(Timeout), TestContext.Current.CancellationToken);

            Assert.Equal("typed locally", await editor.InputValueAsync());
            await AssertNothingSavesAsync(page);

            api.ReleaseDescriptionBatch();
            await WaitUntilAsync(() => api.Descriptions.TryGetValue(taskId, out var text) && text == "typed locally", "The batch was never applied.");

            await AssertNothingSavesAsync(page);
        }
    }

    /// <summary>
    /// Twenty quick keystrokes go in one batch, never one request per keystroke: the first batch is held as it arrives, and
    /// while it is outstanding no second one is sent; once it is released, the batches sent in all are far fewer than the
    /// keystrokes, and together they carry every keystroke's change exactly once.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "AR-19")]
    public async Task Twenty_quick_keystrokes_go_in_one_batch_and_one_batch_is_in_flight_at_a_time()
    {
        var api = OwnerSpace("owner");
        var taskId = Seed(api, "Draft the brief");
        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenPanelAsync(api, page, taskId);

            var held = api.HoldDescriptionBatchAsync();
            var editor = page.Locator($"{Dialog} {Editor}");

            await editor.PressSequentiallyAsync("abcdefghijklmnopqrst");
            await held.WaitAsync(TimeSpan.FromMilliseconds(Timeout), TestContext.Current.CancellationToken);
            await editor.PressSequentiallyAsync("uvwxy");

            Assert.Single(api.DescriptionBatches);

            api.ReleaseDescriptionBatch();
            await WaitUntilAsync(() => api.Descriptions.TryGetValue(taskId, out var text) && text == "abcdefghijklmnopqrstuvwxy", "The keystrokes never all arrived.");

            var changes = api.DescriptionBatches.SelectMany(FakeYelloApi.ChangesIn).ToList();

            Assert.InRange(api.DescriptionBatches.Count, 1, 3);
            Assert.Equal(changes.Count, changes.Distinct(StringComparer.Ordinal).Count());
        }
    }

    /// <summary>
    /// A change the server refuses is withdrawn: the refused text leaves the editor and the text the server holds is what
    /// remains.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "AD-11")]
    [Trait("Requirement", "AR-20")]
    public async Task A_refused_change_is_withdrawn_from_the_editor()
    {
        var api = OwnerSpace("owner");
        var taskId = Seed(api, "Draft the brief");

        api.Descriptions[taskId] = "kept";

        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenPanelAsync(api, page, taskId);

            var editor = page.Locator($"{Dialog} {Editor}");

            await editor.WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });
            Assert.Equal("kept", await editor.InputValueAsync());

            api.RefuseEveryChangeInTheNextBatch = true;
            await editor.PressAsync("End");
            await editor.PressSequentiallyAsync(" refused");
            await editor.BlurAsync();

            await WaitUntilAsync(() => api.DescriptionBatches.Count == 1, "The refused batch was never sent.");
            await Assertions.Expect(editor).ToHaveValueAsync("kept", new LocatorAssertionsToHaveValueOptions { Timeout = Timeout });
        }
    }

    /// <summary>
    /// A batch that fails (a <c>503</c>) is kept and sent again with the next flush: the second batch carries every change of
    /// the first, and nothing new is shown on the page.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "FR-33")]
    public async Task A_failed_batch_is_kept_and_sent_again_with_the_next()
    {
        var api = OwnerSpace("owner");
        var taskId = Seed(api, "Draft the brief");
        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenPanelAsync(api, page, taskId);

            var editor = page.Locator($"{Dialog} {Editor}");
            var before = await page.Locator(Dialog).InnerTextAsync();

            api.FailTheNextBatch = true;
            await editor.PressSequentiallyAsync("first");
            await editor.BlurAsync();
            await WaitUntilAsync(() => api.DescriptionBatches.Count == 1, "The first batch was never sent.");

            await editor.FocusAsync();
            await editor.PressAsync("End");
            await editor.PressSequentiallyAsync(" second");
            await editor.BlurAsync();
            await WaitUntilAsync(() => api.DescriptionBatches.Count == 2, "The second batch was never sent.");

            var first = FakeYelloApi.ChangesIn(api.DescriptionBatches[0]);
            var second = FakeYelloApi.ChangesIn(api.DescriptionBatches[1]);

            Assert.NotEmpty(first);
            Assert.All(first, change => Assert.Contains(change, second));
            Assert.Equal(before, await page.Locator(Dialog).InnerTextAsync());
        }
    }

    /// <summary>
    /// The first <c>Esc</c> in the editor leaves it, sending what is pending, and the panel stays; the second closes the
    /// panel.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "UX-DR39")]
    [Trait("Priority", "P1")]
    public async Task The_first_Esc_leaves_the_editor_and_the_second_closes_the_panel()
    {
        var api = OwnerSpace("owner");
        var taskId = Seed(api, "Draft the brief");
        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenPanelAsync(api, page, taskId);

            var editor = page.Locator($"{Dialog} {Editor}");

            await editor.PressSequentiallyAsync("esc flushes");
            await page.Keyboard.PressAsync("Escape");

            Assert.Equal(1, await page.Locator(Dialog).CountAsync());
            Assert.False(await editor.EvaluateAsync<bool>("e => e === document.activeElement"), "The first Esc did not leave the editor.");
            Assert.True(await page.Locator(Dialog).EvaluateAsync<bool>("d => d.contains(document.activeElement)"), "Focus left the panel.");
            await WaitUntilAsync(() => api.DescriptionBatches.Count >= 1, "Esc did not send what was pending.");

            await page.Keyboard.PressAsync("Escape");
            await page.Locator(Dialog).WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Detached, Timeout = Timeout });
        }
    }

    /// <summary>
    /// A Viewer gets the same region and heading with the text rendered, line breaks kept, and no editor in the DOM: no
    /// textarea, and nothing disabled, read-only or inert in its place.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "X-9")]
    [Trait("Requirement", "UX-DR27")]
    public async Task A_Viewer_reads_the_text_in_the_same_region_with_no_editor_in_the_DOM()
    {
        var api = OwnerSpace("viewer");
        var taskId = Seed(api, "Draft the brief");

        api.Descriptions[taskId] = "first line\nsecond line";

        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenPanelAsync(api, page, taskId);

            var region = page.Locator($"{Dialog} {Region}");

            await region.GetByText("second line").WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

            Assert.Equal(HeadingText, await region.EvaluateAsync<string>("r => document.getElementById(r.getAttribute('aria-labelledby'))?.textContent.trim() ?? ''"));
            Assert.Equal(0, await page.Locator($"{Dialog} textarea:not([data-single-line]), {Dialog} {Editor}, {Dialog} [contenteditable]").CountAsync());
            Assert.Equal(0, await region.Locator("[disabled], [readonly], [inert], [aria-disabled='true'], [aria-readonly='true']").CountAsync());
            Assert.Equal(0, await page.Locator($"{Dialog} [inert]").CountAsync());
            Assert.Equal("pre-wrap", await region.GetByText("second line").EvaluateAsync<string>("e => getComputedStyle(e).whiteSpace"));
        }
    }

    /// <summary>
    /// A Viewer's empty description reads "No description." in the muted text colour.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "X-9")]
    [Trait("Priority", "P1")]
    public async Task A_Viewers_empty_description_reads_No_description_in_the_muted_colour()
    {
        var api = OwnerSpace("viewer");
        var taskId = Seed(api, "Draft the brief");
        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenPanelAsync(api, page, taskId);

            var empty = page.Locator($"{Dialog} {Region}").GetByText(EmptyCopy, new LocatorGetByTextOptions { Exact = true });

            await empty.WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

            Assert.Equal(await TokenColourAsync(page, "--text-muted", "color"), await empty.EvaluateAsync<string>("e => getComputedStyle(e).color"));
        }
    }

    /// <summary>
    /// A Role re-read lower while the panel is open swaps the editor for the rendered text in place: the panel, the region and
    /// its heading stay, and the textarea goes.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "UX-DR37")]
    public async Task A_Role_reread_lower_swaps_the_editor_for_the_text_in_place()
    {
        var api = OwnerSpace("owner");
        var taskId = Seed(api, "Draft the brief");

        api.Descriptions[taskId] = "still readable";

        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenPanelAsync(api, page, taskId);
            await page.Locator($"{Dialog} {Editor}").WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

            api.Roles[Northwind] = "viewer";

            await page.EvaluateAsync("path => Blazor.navigateTo(path)", ClientBoard());
            await page.Locator(RoleChip, new PageLocatorOptions { HasText = "Viewer" }).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });
            await page.Locator($"{Dialog} {Editor}").WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Detached, Timeout = Timeout });

            Assert.Equal(1, await page.Locator(Dialog).CountAsync());
            Assert.Equal(1, await page.Locator($"{Dialog} {Region}").CountAsync());
            Assert.Contains("still readable", await page.Locator($"{Dialog} {Region}").InnerTextAsync(), StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// One signed-in Space at <paramref name="role"/>.
    /// </summary>
    private static FakeYelloApi OwnerSpace(string role)
    {
        var api = new FakeYelloApi { SignedIn = true };

        api.Switcher.Add((Northwind, "Northwind Redesign"));
        api.Roles[Northwind] = role;

        return api;
    }

    /// <summary>
    /// A Project on Space home and its Board, with one Task in the first column; answers its id.
    /// </summary>
    private static Guid Seed(FakeYelloApi api, string title)
    {
        var columns = StatusNames.Select(status => new FakeColumn(Guid.NewGuid(), status, 0, [])).ToList();
        var task = new FakeTask(Guid.NewGuid(), title, columns[0].StatusId, "a00000", DateTimeOffset.UtcNow);

        columns[0].Tasks.Add(task);
        columns[0].Total++;
        api.Boards[(Northwind, Atlas)] = new FakeBoard(new FakeBoardProject(Atlas, "Atlas"), columns);
        api.Projects[Northwind] = [new FakeProject(Atlas, "Atlas", DateTimeOffset.UtcNow)];

        return task.Id;
    }

    /// <summary>
    /// The computed colour a token resolves to on this page, read from a probe element styled with it.
    /// </summary>
    private static Task<string> TokenColourAsync(IPage page, string token, string property = "background-color") =>
        page.EvaluateAsync<string>(
            "([token, property]) => { const p = document.createElement('div'); p.style.setProperty(property, `var(${token})`); document.body.append(p); const c = getComputedStyle(p).getPropertyValue(property); p.remove(); return c; }",
            new[] { token, property });

    private static async Task AssertNothingSavesAsync(IPage page)
    {
        var text = await page.Locator("body").InnerTextAsync();

        Assert.All(Banned, word => Assert.DoesNotContain(word, text, StringComparison.OrdinalIgnoreCase));
        Assert.Equal(0, await page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { NameRegex = new Regex("save|merge", RegexOptions.IgnoreCase, TimeSpan.FromSeconds(1)) }).CountAsync());
        Assert.Equal(0, await page.Locator($"{Dialog} [role='progressbar'], {Dialog} [aria-busy='true']").CountAsync());
    }

    /// <summary>
    /// Polls a condition with a deadline, never a sleep.
    /// </summary>
    private static async Task WaitUntilAsync(Func<bool> condition, string because)
    {
        var deadline = DateTimeOffset.UtcNow.AddMilliseconds(Timeout);

        while (!condition())
        {
            Assert.True(DateTimeOffset.UtcNow < deadline, because);
            await Task.Yield();
        }
    }

    private static string ClientBoard() => $"/spaces/{Northwind:D}/projects/{Atlas:D}";

    private async Task OpenPanelAsync(FakeYelloApi api, IPage page, Guid taskId)
    {
        await api.InstallAsync(page);
        await page.GotoAsync(new Uri(surface.BaseAddress, ClientBoard().TrimStart('/')).ToString());
        await page.Locator(RoleChip).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });
        await page.Locator($"{Card} button[data-task-id='{taskId:D}']").ClickAsync();
        await page.Locator(Dialog).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });
        await page.Locator($"{Dialog} {Region}").WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });
    }
}
