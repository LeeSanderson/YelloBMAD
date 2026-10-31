using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Playwright;
using Xunit;
using Yello.Contracts.Projects;
using Yello.Contracts.Spaces;
using Yello.Contracts.Tasks;
using Yello.Tests.Shared;

namespace Yello.Tests.Client;

/// <summary>
/// Story 3.4 AC7 against the real Host (written by its ATDD run, 2026-10-31): the client manufactures no
/// <c>AccessRefusal</c> during ordinary use by an Account holding two populated Spaces, because a client that does buries
/// real probing in its own noise (<c>EXPERIENCE.md:146</c>). The record is read from the database the real Host writes.
/// </summary>
/// <remarks>
/// <para>
/// <b>AC6, Q4 (a): the API counterpart</b> is the record-kind rows: AC3's <c>ISO-TOKEN-OWN-OTHER-SPACE-*</c>
/// (<c>CrossSpace</c>) and AC2's <c>ISO-OWNER-ELSEWHERE-*</c> (<c>InsufficientRole</c>, C1).
/// </para>
/// <para>
/// <b>The second Space is created through the product</b> (Q10 (a)), so it carries its default Statuses and its Board
/// renders; <c>RealHostFixture.AddSecondSpaceAsync</c> (SQL, no Statuses) stays for the cases that use it.
/// </para>
/// <para>
/// <b>No new container class.</b> The case joins <see cref="RealHostCollection"/> and its one Host and database.
/// </para>
/// </remarks>
/// <param name="world">The real-Host world.</param>
[Trait("Suite", "Client")]
[Trait("Priority", "P0")]
[Trait("Requirement", "AD-20")]
[Trait("Requirement", "NFR-7")]
[Trait("Requirement", "SM-1")]
[Collection(nameof(RealHostCollection))]
public sealed class RealHostRefusalNoiseTests(RealHostFixture world)
{
    private const float Timeout = 30_000;
    private const string RoleChip = "nav.context-bar .role-chip";
    private const string SwitcherTrigger = "nav.context-bar button[aria-controls]";

    /// <summary>
    /// AC7 - one Account with two populated Spaces visits each Space's home, Board, List and settings, switches both ways
    /// through the switcher, goes Back and Forward, and follows a deep link to its own other Space: no refusal is recorded
    /// against it. Anchor: one direct <c>fetch</c> from the page to another Account's Space raises the count to exactly 1.
    /// Plant (the dev phase's): bypass <c>SpaceDirectory.Lists</c> in <c>SpaceArrival.razor</c> and deep-link to another
    /// Account's Space - the count must not stay 0.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Ordinary_use_of_two_owned_Spaces_records_no_refusal_and_one_probe_records_one()
    {
        RealHostFixture.SkipUnlessAnEngineIsConfigured();

        var actor = await world.NewActorAsync();
        var second = await CreateSecondSpaceAsync(actor);
        var firstProject = await SeedProjectWithTaskAsync(actor, actor.SpaceId);
        var secondProject = await SeedProjectWithTaskAsync(actor, second);
        var (context, page) = await world.OpenSignedInAsync(actor);

        await using (context)
        {
            await page.Locator(RoleChip).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

            foreach (var (space, project) in new[] { (actor.SpaceId, firstProject), (second, secondProject) })
            {
                foreach (var path in new[] { $"/spaces/{space:D}", $"/spaces/{space:D}/projects/{project:D}", $"/spaces/{space:D}/projects/{project:D}/list", $"/spaces/{space:D}/settings" })
                {
                    await page.EvaluateAsync("path => Blazor.navigateTo(path)", path);
                    await WaitForPathAsync(page, path);
                    await page.Locator(RoleChip).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });
                }
            }

            await ChooseInSwitcherAsync(page, actor.SpaceName);
            await WaitForPathAsync(page, $"/spaces/{actor.SpaceId:D}");
            await ChooseInSwitcherAsync(page, SecondName(actor));
            await WaitForPathAsync(page, $"/spaces/{second:D}");

            await page.GoBackAsync();
            await WaitForPathAsync(page, $"/spaces/{actor.SpaceId:D}");
            await page.GoForwardAsync();
            await WaitForPathAsync(page, $"/spaces/{second:D}");

            await page.GotoAsync(new Uri(world.ClientAddress, $"spaces/{actor.SpaceId:D}").ToString());
            await page.Locator(RoleChip).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

            Assert.Equal(0, await world.Database.RefusalCountAsync(actor.AccountId));

            // The anchor: a request the client would never make - another Account's Space - is recorded, once.
            var status = await page.EvaluateAsync<int>(
                "async url => (await fetch(url, { credentials: 'include' })).status",
                new Uri(world.Host.Address, SpaceRoutes.ActiveOf(world.B.SpaceId)).ToString());

            Assert.Equal(404, status);
            Assert.Equal(1, await world.Database.RefusalCountAsync(actor.AccountId));
        }
    }

    private static string SecondName(ProductActor actor) => $"{actor.DisplayName} two";

    private static async Task<Guid> CreateSecondSpaceAsync(ProductActor actor)
    {
        var name = SecondName(actor);

        actor.HoldsSpaceNamed(name);

        var created = await actor.SendAsync(IsolationSurface.Session, HttpMethod.Post, SpaceRoutes.Directory, JsonContent.Create(new { name }), headers: null);

        Assert.True(created.StatusCode == HttpStatusCode.Created, $"Creating a second Space through the product: {created.StatusCode} {created.Body}");

        using var document = JsonDocument.Parse(created.Body);

        return document.RootElement.GetProperty("id").GetGuid();
    }

    private static async Task<Guid> SeedProjectWithTaskAsync(ProductActor actor, Guid spaceId)
    {
        var name = $"Project {Guid.NewGuid():N}";
        var title = $"Task {Guid.NewGuid():N}";

        actor.HoldsProjectNamed(name);
        actor.HoldsTaskTitled(title);

        var project = await actor.SendAsync(IsolationSurface.Session, HttpMethod.Post, ProjectRoutes.ListIn(spaceId), JsonContent.Create(new { name }), headers: null);

        Assert.True(project.StatusCode == HttpStatusCode.Created, $"{project.StatusCode} {project.Body}");

        using var document = JsonDocument.Parse(project.Body);
        var projectId = document.RootElement.GetProperty("id").GetGuid();
        var task = await actor.SendAsync(IsolationSurface.Session, HttpMethod.Post, TaskRoutes.CreateIn(spaceId, projectId), JsonContent.Create(new { title }), headers: null);

        Assert.True(task.StatusCode == HttpStatusCode.Created, $"{task.StatusCode} {task.Body}");

        return projectId;
    }

    private static async Task ChooseInSwitcherAsync(IPage page, string name)
    {
        var trigger = page.Locator(SwitcherTrigger);
        var list = await trigger.GetAttributeAsync("aria-controls");

        if (await trigger.GetAttributeAsync("aria-expanded") != "true")
        {
            await trigger.ClickAsync();
        }

        var rows = page.Locator($"[id='{list}'] a");

        await rows.First.WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });
        await rows.Filter(new LocatorFilterOptions { HasText = name }).ClickAsync();
    }

    private static Task WaitForPathAsync(IPage page, string path) =>
        page.WaitForFunctionAsync("path => location.pathname === path", path, new PageWaitForFunctionOptions { Timeout = Timeout, PollingInterval = 50 });
}
