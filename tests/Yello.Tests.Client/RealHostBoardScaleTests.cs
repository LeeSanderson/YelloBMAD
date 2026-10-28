using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Playwright;
using Xunit;
using Yello.Contracts.Projects;
using Yello.Contracts.Spaces;
using Yello.Tests.Shared;
using static Yello.Tests.Client.KeyboardHarness;

namespace Yello.Tests.Client;

/// <summary>
/// Story 2.9's real-Host keyboard traversal at the NFR-8 scale, Nightly (X-5's deep half; test design R4,
/// <c>test-design-architecture.md:467</c>): a column of 5,000 real Tasks, traversed from the first Task to the last by
/// <c>↓</c> alone through the real client, the real Host and the real column-page route, with focus asserted on the intended
/// Task at every step. Written red by the story 2.9 ATDD run.
/// </summary>
/// <remarks>
/// <para>
/// <b>Seeding</b>: the Account, its Project and its Statuses through the product; the 5,000 Tasks by one set-based statement
/// (<see cref="WireDatabase.SeedTasksAsync"/>, whose remarks state every constraint it satisfies) - 5,000 creations over
/// HTTP would test the bound lock, not the Board. A fresh Account, so the collection's A and B are untouched.
/// </para>
/// <para>
/// <b>Nightly only</b> (<c>Tier=Nightly</c>): 4,999 key presses, each polled to its focus, take minutes.
/// </para>
/// </remarks>
/// <param name="world">The real Host, the dev server and the browser.</param>
[Trait("Suite", "Client")]
[Trait("Priority", "P0")]
[Trait("Tier", "Nightly")]
[Trait("Requirement", "AD-29")]
[Trait("Requirement", "NFR-8")]
[Collection(nameof(RealHostCollection))]
public sealed class RealHostBoardScaleTests(RealHostFixture world)
{
    private const int Scale = 5_000;
    private const float Timeout = 30_000;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// X-5 (P0, AC7, AC11, AC5) - from the first of 5,000 Tasks, <c>↓</c> 4,999 times lands focus on Seeded 2, 3, … 5,000 in
    /// turn, the client fetching each next page from the real Host as it reaches the boundary; at the end all 5,000 cards
    /// are drawn, each once.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Explicit = true)]
    public async Task Down_from_the_first_of_5000_real_Tasks_reaches_the_last_with_focus_on_each_in_turn()
    {
        RealHostFixture.SkipUnlessAnEngineIsConfigured();

        var actor = await world.NewActorAsync();
        var created = await actor.SendAsync(
            IsolationSurface.Session,
            HttpMethod.Post,
            ProjectRoutes.ListIn(actor.SpaceId),
            JsonContent.Create(new { name = "Five thousand, by keyboard" }, options: JsonOptions),
            headers: null);

        Assert.True(created.StatusCode == HttpStatusCode.Created, $"{created.StatusCode} {created.Body}");

        Guid projectId;
        Guid statusId;

        using (var project = JsonDocument.Parse(created.Body))
        {
            projectId = project.RootElement.GetProperty("id").GetGuid();
        }

        var statuses = await actor.SendAsync(IsolationSurface.Session, HttpMethod.Get, SpaceRoutes.StatusesOf(actor.SpaceId));

        using (var document = JsonDocument.Parse(statuses.Body))
        {
            statusId = document.RootElement.EnumerateArray().First().GetProperty("id").GetGuid();
        }

        await world.Database.SeedTasksAsync(actor.SpaceId, projectId, statusId, Scale);

        var (context, page) = await world.OpenSignedInAsync(actor);

        await using (context)
        {
            await page.Locator("nav.context-bar .role-chip").WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });
            await page.EvaluateAsync("path => Blazor.navigateTo(path)", $"/spaces/{actor.SpaceId:D}/projects/{projectId:D}");
            await page.Locator(".task-card").First.WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });
            await WaitForKeysAsync(page);
            await FocusCardAsync(page, "Seeded 1");

            for (var ordinal = 2; ordinal <= Scale; ordinal++)
            {
                await PressAndFocusAsync(page, "ArrowDown", $"Seeded {ordinal}");
            }

            var titles = await page.Locator("main .board-column").First.Locator(".task-card__title").AllTextContentsAsync();

            Assert.Equal(Scale, titles.Count);
            Assert.Equal(Scale, titles.Select(title => title.Trim()).Distinct(StringComparer.Ordinal).Count());
        }
    }
}
