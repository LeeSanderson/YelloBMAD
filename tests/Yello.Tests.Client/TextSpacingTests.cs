using Microsoft.Playwright;
using Xunit;
using Yello.Contracts.Labels;

namespace Yello.Tests.Client;

/// <summary>
/// WCAG 1.4.12 text spacing and 200% text-only zoom on the two Space-scoped surfaces - Space home and the
/// Board: with either applied, no text is clipped by its box, no Task card overlaps the next, and the page
/// does not scroll sideways. X-11; <c>deferred-work.md:20</c>, owned by "the first story with a real
/// surface" and taken by story 2.2, whose Board is the product's primary surface. NFR-9.
/// </summary>
/// <remarks>
/// <para>
/// <b>The override is the one 1.4.12 names</b>: line-height 1.5, paragraph spacing 2em, letter-spacing
/// 0.12em and word-spacing 0.16em, applied to everything with <c>!important</c>, as the bookmarklets audits use
/// do. <b>Text-only zoom</b> is the root font size at 200%: every type size here is <c>rem</c>
/// (<c>Type_is_never_sized_in_absolute_pixels</c>), so that is what a browser's text-only zoom changes.
/// </para>
/// <para>
/// <b>Clipped</b> means a box that hides overflow - <c>overflow: hidden</c> or <c>clip</c> - whose content is
/// larger than it. A box that scrolls (a column, the row of columns) is not clipping: its content is
/// reachable. Visually hidden text is skipped; it is clipped on purpose.
/// </para>
/// </remarks>
[Trait("Suite", "Client")]
[Trait("Priority", "P1")]
[Trait("Requirement", "NFR-9")]
[Collection(nameof(ClientSurfaceCollection))]
public sealed class TextSpacingTests(ClientSurfaceFixture surface)
{
    private const float Timeout = 30_000;
    private const string EnglishLocale = "en-GB";

    private const string TextSpacingOverride =
        "* { line-height: 1.5 !important; letter-spacing: 0.12em !important; word-spacing: 0.16em !important; } "
        + "p { margin-block-end: 2em !important; }";

    private const string TextOnlyZoom = "html { font-size: 200% !important; }";

    private const string ClippedOrOverlapping =
        """
        () => {
            const problems = [];
            for (const element of document.querySelectorAll('body *')) {
                if (element.closest('.visually-hidden, .skip-control, [aria-hidden="true"]')) continue;
                const style = getComputedStyle(element);
                if (style.display === 'none') continue;
                const clipsX = ['hidden', 'clip'].includes(style.overflowX);
                const clipsY = ['hidden', 'clip'].includes(style.overflowY);
                if ((clipsX && element.scrollWidth > element.clientWidth + 1) || (clipsY && element.scrollHeight > element.clientHeight + 1)) {
                    problems.push('clipped: ' + element.tagName + '.' + element.className);
                }
            }
            for (const list of document.querySelectorAll('.board-column__cards')) {
                const cards = [...list.querySelectorAll('.task-card')].map(card => card.getBoundingClientRect());
                for (let index = 1; index < cards.length; index++) {
                    if (cards[index].top < cards[index - 1].bottom - 1) problems.push('overlapping cards');
                }
            }
            if (document.documentElement.scrollWidth > document.documentElement.clientWidth + 1) {
                problems.push('the page scrolls sideways');
            }
            return problems;
        }
        """;

    private static readonly Guid Northwind = Guid.NewGuid();
    private static readonly Guid Atlas = Guid.NewGuid();

    /// <summary>
    /// 1.4.12 and 200% text-only zoom on the Board (with long titles in every column) and on Space home: no
    /// text clipped, no card overlapping the next, no sideways page scroll. Anchored: the surface's text is on
    /// screen before the check runs.
    /// </summary>
    /// <param name="onBoard">Whether the surface is the Board (otherwise Space home).</param>
    /// <param name="zoom">Whether the adaptation is 200% text-only zoom (otherwise 1.4.12 text spacing).</param>
    /// <returns>A task.</returns>
    [Theory]
    [InlineData(true, false)]
    [InlineData(true, true)]
    [InlineData(false, false)]
    [InlineData(false, true)]
    public async Task Text_spacing_and_text_only_zoom_clip_nothing(bool onBoard, bool zoom)
    {
        var api = Seeded();
        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await page.SetViewportSizeAsync(1280, 900);
            await api.InstallAsync(page);
            await page.GotoAsync(new Uri(surface.BaseAddress, onBoard ? $"spaces/{Northwind:D}/projects/{Atlas:D}" : $"spaces/{Northwind:D}").ToString());

            var anchor = onBoard
                ? page.Locator(".task-card", new PageLocatorOptions { HasText = "Prepare the quarterly" }).First
                : page.Locator("main").GetByRole(AriaRole.Link, new LocatorGetByRoleOptions { Name = "Atlas" });

            await anchor.WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });
            await page.AddStyleTagAsync(new PageAddStyleTagOptions { Content = zoom ? TextOnlyZoom : TextSpacingOverride });

            var problems = await page.EvaluateAsync<string[]>(ClippedOrOverlapping);

            Assert.True(problems.Length == 0, string.Join("; ", problems));
        }
    }

    /// <summary>
    /// Story 2.3, AC13, <c>deferred-work.md:20</c>'s convention that every new surface adds its own case: the Task
    /// detail panel, open on a long title with a due date, under 1.4.12 text spacing and 200% text-only zoom -
    /// no text in it clipped, no sideways page scroll. Anchored: the panel's heading is on screen before the
    /// check runs. Written red by the story 2.3 ATDD run; the opening control is Q7 (a)'s
    /// <c>button[data-task-id]</c> in the card.
    /// </summary>
    /// <param name="zoom">Whether the adaptation is 200% text-only zoom (otherwise 1.4.12 text spacing).</param>
    /// <returns>A task.</returns>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    [Trait("Requirement", "UX-DR10")]
    public async Task Text_spacing_and_text_only_zoom_clip_nothing_in_the_Task_detail_panel(bool zoom)
    {
        var api = Seeded();
        var (column, task) = api.FindTask(api.Boards[(Northwind, Atlas)].Columns[0].Tasks[0].Id)!.Value;

        column.Tasks[0] = task with { DueDate = new DateTimeOffset(2026, 11, 3, 0, 0, 0, TimeSpan.Zero) };

        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await page.SetViewportSizeAsync(1280, 900);
            await api.InstallAsync(page);
            await page.GotoAsync(new Uri(surface.BaseAddress, $"spaces/{Northwind:D}/projects/{Atlas:D}").ToString());

            await page.Locator($".task-card button[data-task-id='{task.Id:D}']").ClickAsync(new LocatorClickOptions { Timeout = Timeout });
            await page.Locator("[role='dialog']").GetByRole(AriaRole.Heading, new LocatorGetByRoleOptions { Name = task.Title })
                .WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });
            await page.AddStyleTagAsync(new PageAddStyleTagOptions { Content = zoom ? TextOnlyZoom : TextSpacingOverride });

            var problems = await page.EvaluateAsync<string[]>(ClippedOrOverlapping);

            Assert.True(problems.Length == 0, string.Join("; ", problems));
        }
    }

    /// <summary>
    /// Story 2.4, AC13, UX-DR40, <c>deferred-work.md:20</c>'s convention that every new surface adds its own case: a Task
    /// card carrying four long Labels (its metadata row wrapped), and the Labels section of Space settings, under 1.4.12
    /// text spacing and 200% text-only zoom - no chip text clipped or overlapped, no sideways page scroll. Anchored: a
    /// chip, or the Label list, is on screen before the check runs. Written red by the story 2.4 ATDD run; the locators
    /// are its assumptions (<c>.label-chip</c>, Q7's <c>/spaces/{id}/settings</c>).
    /// </summary>
    /// <param name="onSettings">Whether the surface is Space settings (otherwise the Board's labelled card).</param>
    /// <param name="zoom">Whether the adaptation is 200% text-only zoom (otherwise 1.4.12 text spacing).</param>
    /// <returns>A task.</returns>
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    [Trait("Requirement", "UX-DR40")]
    public async Task Text_spacing_and_text_only_zoom_clip_no_Label_chip(bool onSettings, bool zoom)
    {
        var api = Seeded();
        var names = Enumerable.Range(1, 4).Select(index => $"A rather long Label name, number {index}").ToList();
        var labels = names.Select((name, index) => new FakeLabel(Guid.NewGuid(), name, LabelColours.Keys[index])).ToList();
        var (column, task) = api.FindTask(api.Boards[(Northwind, Atlas)].Columns[0].Tasks[0].Id)!.Value;

        api.Labels[Northwind] = labels;
        column.Tasks[0] = task with { LabelIds = [.. labels.OrderBy(label => label.Name, StringComparer.OrdinalIgnoreCase).Select(label => label.Id)] };

        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await page.SetViewportSizeAsync(1280, 900);
            await api.InstallAsync(page);
            await page.GotoAsync(new Uri(surface.BaseAddress, onSettings ? $"spaces/{Northwind:D}/settings" : $"spaces/{Northwind:D}/projects/{Atlas:D}").ToString());

            var anchor = onSettings
                ? page.Locator("main li", new PageLocatorOptions { HasText = names[0] }).First
                : page.Locator(".task-card .label-chip").First;

            await anchor.WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });
            await page.AddStyleTagAsync(new PageAddStyleTagOptions { Content = zoom ? TextOnlyZoom : TextSpacingOverride });

            var problems = await page.EvaluateAsync<string[]>(ClippedOrOverlapping);
            var overlappingChips = await page.EvaluateAsync<int>(
                """
                () => {
                    let overlaps = 0;
                    for (const row of document.querySelectorAll('.task-card__meta')) {
                        const chips = [...row.querySelectorAll('.label-chip')].map(chip => chip.getBoundingClientRect());
                        for (let a = 0; a < chips.length; a++)
                            for (let b = a + 1; b < chips.length; b++)
                                if (chips[a].left < chips[b].right - 1 && chips[b].left < chips[a].right - 1 && chips[a].top < chips[b].bottom - 1 && chips[b].top < chips[a].bottom - 1) overlaps++;
                    }
                    return overlaps;
                }
                """);

            Assert.True(problems.Length == 0, string.Join("; ", problems));
            Assert.Equal(0, overlappingChips);
        }
    }

    /// <summary>
    /// Story 2.4, AC13 ("a Label chip on a card and in the panel"), UX-DR40 - the Task detail panel open on a Task
    /// carrying four long Labels, under 1.4.12 text spacing and 200% text-only zoom: no chip text clipped, no two chips
    /// overlapping, no sideways page scroll. Anchored: a chip is on screen in the panel before the check runs. Added at
    /// code review: the card and Space settings had their case, the panel did not.
    /// </summary>
    /// <param name="zoom">Whether the adaptation is 200% text-only zoom (otherwise 1.4.12 text spacing).</param>
    /// <returns>A task.</returns>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    [Trait("Requirement", "UX-DR40")]
    public async Task Text_spacing_and_text_only_zoom_clip_no_Label_chip_in_the_Task_detail_panel(bool zoom)
    {
        var api = Seeded();
        var names = Enumerable.Range(1, 4).Select(index => $"A rather long Label name, number {index}").ToList();
        var labels = names.Select((name, index) => new FakeLabel(Guid.NewGuid(), name, LabelColours.Keys[index])).ToList();
        var (column, task) = api.FindTask(api.Boards[(Northwind, Atlas)].Columns[0].Tasks[0].Id)!.Value;

        api.Labels[Northwind] = labels;
        column.Tasks[0] = task with { LabelIds = [.. labels.OrderBy(label => label.Name, StringComparer.OrdinalIgnoreCase).Select(label => label.Id)] };

        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await page.SetViewportSizeAsync(1280, 900);
            await api.InstallAsync(page);
            await page.GotoAsync(new Uri(surface.BaseAddress, $"spaces/{Northwind:D}/projects/{Atlas:D}").ToString());

            await page.Locator($".task-card button[data-task-id='{task.Id:D}']").ClickAsync(new LocatorClickOptions { Timeout = Timeout });
            await page.Locator("[role='dialog'] .label-chip").First.WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });
            await page.AddStyleTagAsync(new PageAddStyleTagOptions { Content = zoom ? TextOnlyZoom : TextSpacingOverride });

            var problems = await page.EvaluateAsync<string[]>(ClippedOrOverlapping);
            var chips = await page.EvaluateAsync<int[]>(
                """
                () => {
                    const chips = [...document.querySelectorAll("[role='dialog'] .label-chip")].map(chip => chip.getBoundingClientRect());
                    let overlaps = 0;
                    for (let a = 0; a < chips.length; a++)
                        for (let b = a + 1; b < chips.length; b++)
                            if (chips[a].left < chips[b].right - 1 && chips[b].left < chips[a].right - 1 && chips[a].top < chips[b].bottom - 1 && chips[b].top < chips[a].bottom - 1) overlaps++;
                    return [chips.length, overlaps];
                }
                """);

            Assert.True(problems.Length == 0, string.Join("; ", problems));
            Assert.Equal(4, chips[0]);
            Assert.Equal(0, chips[1]);
        }
    }

    /// <summary>
    /// Story 2.5, AC13, <c>deferred-work.md:20</c> (each new surface adds its own case) - the Task confirm, in place in the
    /// detail panel, for a Task with a long title, under 1.4.12 text spacing and 200% text-only zoom: no text clipped and no
    /// sideways page scroll. Anchored: the confirm is on screen before the check runs. Written red by the story 2.5 ATDD
    /// run; the locators are <c>DeleteTests</c>' assumptions ("Delete Task", <c>.destructive-confirm</c>).
    /// </summary>
    /// <param name="zoom">Whether the adaptation is 200% text-only zoom (otherwise 1.4.12 text spacing).</param>
    /// <returns>A task.</returns>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    [Trait("Requirement", "UX-DR40")]
    [Trait("Requirement", "UX-DR12")]
    public async Task Text_spacing_and_text_only_zoom_clip_nothing_in_the_Task_delete_confirm(bool zoom)
    {
        var api = Seeded();
        var task = api.Boards[(Northwind, Atlas)].Columns[0].Tasks[0];
        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await page.SetViewportSizeAsync(1280, 900);
            await api.InstallAsync(page);
            await page.GotoAsync(new Uri(surface.BaseAddress, $"spaces/{Northwind:D}/projects/{Atlas:D}").ToString());

            await page.Locator($".task-card button[data-task-id='{task.Id:D}']").ClickAsync(new LocatorClickOptions { Timeout = Timeout });
            await page.Locator("[role='dialog']").GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "Delete Task", Exact = true }).ClickAsync(new LocatorClickOptions { Timeout = Timeout });
            await page.Locator("[role='dialog'] .destructive-confirm").WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });
            await page.AddStyleTagAsync(new PageAddStyleTagOptions { Content = zoom ? TextOnlyZoom : TextSpacingOverride });

            var problems = await page.EvaluateAsync<string[]>(ClippedOrOverlapping);

            Assert.True(problems.Length == 0, string.Join("; ", problems));
        }
    }

    /// <summary>
    /// Story 2.5, AC13, <c>deferred-work.md:20</c> - the Project confirm, in place in its row on Space home, for a Project
    /// with a long name holding Tasks, under 1.4.12 text spacing and 200% text-only zoom: no text clipped and no sideways
    /// page scroll. Anchored: the confirm is on screen before the check runs. Written red by the story 2.5 ATDD run.
    /// </summary>
    /// <param name="zoom">Whether the adaptation is 200% text-only zoom (otherwise 1.4.12 text spacing).</param>
    /// <returns>A task.</returns>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    [Trait("Requirement", "UX-DR40")]
    [Trait("Requirement", "UX-DR12")]
    public async Task Text_spacing_and_text_only_zoom_clip_nothing_in_the_Project_delete_confirm(bool zoom)
    {
        const string longName = "Prepare the quarterly planning document for the infrastructure review";

        var api = Seeded();

        api.ServeProjectTaskCounts = true;
        api.Projects[Northwind] = [new FakeProject(Atlas, longName, DateTimeOffset.UtcNow)];

        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await page.SetViewportSizeAsync(1280, 900);
            await api.InstallAsync(page);
            await page.GotoAsync(new Uri(surface.BaseAddress, $"spaces/{Northwind:D}").ToString());

            var row = page.Locator("main li", new PageLocatorOptions { HasText = longName });

            await row.GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "Delete Project" }).ClickAsync(new LocatorClickOptions { Timeout = Timeout });
            await row.Locator(".destructive-confirm").WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });
            await page.AddStyleTagAsync(new PageAddStyleTagOptions { Content = zoom ? TextOnlyZoom : TextSpacingOverride });

            var problems = await page.EvaluateAsync<string[]>(ClippedOrOverlapping);

            Assert.True(problems.Length == 0, string.Join("; ", problems));
        }
    }

    /// <summary>
    /// Story 2.6, AC14, <c>deferred-work.md:20</c> (each new surface adds its own case) - the Move picker, in place in the
    /// Task detail panel at its position step for a column of long titles, under 1.4.12 text spacing and 200% text-only
    /// zoom: no option text clipped and no sideways page scroll. Anchored: the position options are on screen before the
    /// check runs. Written red by the story 2.6 ATDD run; the locators are <c>MoveTests</c>' assumptions (a "Move" button,
    /// <c>.move-picker [role=option]</c>, "Top of column").
    /// </summary>
    /// <param name="zoom">Whether the adaptation is 200% text-only zoom (otherwise 1.4.12 text spacing).</param>
    /// <returns>A task.</returns>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    [Trait("Requirement", "UX-DR21")]
    [Trait("Requirement", "UX-DR11")]
    public async Task Text_spacing_and_text_only_zoom_clip_nothing_in_the_Move_picker(bool zoom)
    {
        var api = Seeded();
        var task = api.Boards[(Northwind, Atlas)].Columns[0].Tasks[1];
        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await page.SetViewportSizeAsync(1280, 900);
            await api.InstallAsync(page);
            await page.GotoAsync(new Uri(surface.BaseAddress, $"spaces/{Northwind:D}/projects/{Atlas:D}").ToString());

            await page.Locator($".task-card button[data-task-id='{task.Id:D}']").ClickAsync(new LocatorClickOptions { Timeout = Timeout });
            await page.Locator("[role='dialog']").GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "Move", Exact = true }).ClickAsync(new LocatorClickOptions { Timeout = Timeout });
            await page.Locator(".move-picker [role='option']", new PageLocatorOptions { HasText = "Doing" }).First.ClickAsync(new LocatorClickOptions { Timeout = Timeout });
            await page.Locator(".move-picker [role='option']", new PageLocatorOptions { HasText = "Top of column" }).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });
            await page.AddStyleTagAsync(new PageAddStyleTagOptions { Content = zoom ? TextOnlyZoom : TextSpacingOverride });

            var problems = await page.EvaluateAsync<string[]>(ClippedOrOverlapping);

            Assert.True(problems.Length == 0, string.Join("; ", problems));
        }
    }

    /// <summary>
    /// Story 2.6, AC14, <c>deferred-work.md:20</c> - a card's context menu, opened from its actions control on a card with a
    /// long title, under 1.4.12 text spacing and 200% text-only zoom: no menu text clipped, no card overlapping the next,
    /// and no sideways page scroll. Anchored: the menu is on screen before the check runs. Written red by the story 2.6
    /// ATDD run (Q6 (a): "Actions for {title}", <c>[role=menu]</c>).
    /// </summary>
    /// <param name="zoom">Whether the adaptation is 200% text-only zoom (otherwise 1.4.12 text spacing).</param>
    /// <returns>A task.</returns>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    [Trait("Requirement", "UX-DR21")]
    [Trait("Requirement", "UX-DR24")]
    public async Task Text_spacing_and_text_only_zoom_clip_nothing_in_a_cards_menu(bool zoom)
    {
        var api = Seeded();
        var task = api.Boards[(Northwind, Atlas)].Columns[0].Tasks[0];
        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await page.SetViewportSizeAsync(1280, 900);
            await api.InstallAsync(page);
            await page.GotoAsync(new Uri(surface.BaseAddress, $"spaces/{Northwind:D}/projects/{Atlas:D}").ToString());

            await page.Locator(".task-card").GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = $"Actions for {task.Title}", Exact = true }).First.ClickAsync(new LocatorClickOptions { Timeout = Timeout });
            await page.Locator("[role='menu']").WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });
            await page.AddStyleTagAsync(new PageAddStyleTagOptions { Content = zoom ? TextOnlyZoom : TextSpacingOverride });

            var problems = await page.EvaluateAsync<string[]>(ClippedOrOverlapping);

            Assert.True(problems.Length == 0, string.Join("; ", problems));
        }
    }

    /// <summary>
    /// Story 2.7, <c>deferred-work.md:20</c>'s convention that every new surface adds its own case: mid-drag, the lifted
    /// card and the drop zone under 1.4.12 text spacing and 200% text-only zoom - no text in either clipped, no card in the
    /// list overlapping the next (the lifted card, which travels over the others by design, aside), no sideways page
    /// scroll. Anchored: the drop zone is on screen before the check runs.
    /// </summary>
    /// <param name="zoom">Whether the adaptation is 200% text-only zoom (otherwise 1.4.12 text spacing).</param>
    /// <returns>A task.</returns>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    [Trait("Requirement", "UX-DR22")]
    public async Task Text_spacing_and_text_only_zoom_clip_nothing_mid_drag(bool zoom)
    {
        var api = Seeded();
        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await page.SetViewportSizeAsync(1280, 900);
            await api.InstallAsync(page);
            await page.GotoAsync(new Uri(surface.BaseAddress, $"spaces/{Northwind:D}/projects/{Atlas:D}").ToString());
            await page.Locator(".task-card").First.WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });
            await page.AddStyleTagAsync(new PageAddStyleTagOptions { Content = zoom ? TextOnlyZoom : TextSpacingOverride });

            var grab = await DragHarness.PointInAsync(page.Locator("main .board-column").First.Locator(".task-card__title").Last, 0.5f);

            await page.Mouse.MoveAsync(grab.X, grab.Y);
            await page.Mouse.DownAsync();
            await page.Mouse.MoveAsync(grab.X, grab.Y + 16, new MouseMoveOptions { Steps = 4 });
            await page.Locator(".task-card--lifted").WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

            var doing = page.Locator("main .board-column").Nth(1).Locator(".task-card").Last;

            await DragHarness.MoveMouseToAsync(page, await DragHarness.PointInAsync(doing, 0.8f));
            await page.Locator(".drop-zone").WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

            var problems = await page.EvaluateAsync<string[]>(ClippedOrOverlapping.Replace("querySelectorAll('.task-card')", "querySelectorAll('.task-card:not(.task-card--lifted)')", StringComparison.Ordinal));

            await page.Keyboard.PressAsync("Escape");
            await page.Mouse.UpAsync();

            Assert.True(problems.Length == 0, string.Join("; ", problems));
        }
    }

    private static FakeYelloApi Seeded()
    {
        var api = new FakeYelloApi { SignedIn = true };

        api.Switcher.Add((Northwind, "Northwind Redesign"));
        api.Roles[Northwind] = "owner";

        string[] statuses = ["Todo", "Doing", "Done"];
        var columns = statuses.Select(status => new FakeColumn(Guid.NewGuid(), status, 0, [])).ToList();

        foreach (var column in columns)
        {
            foreach (var title in new[] { "Prepare the quarterly planning document for the infrastructure review", "Short one" })
            {
                column.Tasks.Add(new FakeTask(Guid.NewGuid(), title, column.StatusId, $"a{column.Tasks.Count:D5}", DateTimeOffset.UtcNow));
                column.Total++;
            }
        }

        api.Boards[(Northwind, Atlas)] = new FakeBoard(new FakeBoardProject(Atlas, "Atlas"), columns);
        api.Projects[Northwind] = [new FakeProject(Atlas, "Atlas", DateTimeOffset.UtcNow)];

        return api;
    }
}
