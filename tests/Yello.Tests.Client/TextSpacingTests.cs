using Microsoft.Playwright;
using Xunit;

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
