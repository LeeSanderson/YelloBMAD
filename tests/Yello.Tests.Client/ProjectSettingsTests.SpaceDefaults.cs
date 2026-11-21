using Microsoft.Playwright;
using Xunit;

namespace Yello.Tests.Client;

/// <summary>
/// The Project settings editor stays true after a change at Space level: it reads the Space's defaults again with every set
/// it renders, so the "Add back" list never offers a stale name.
/// </summary>
public sealed partial class ProjectSettingsTests
{
    /// <summary>
    /// The Project removed "Doing", so "Add back" lists it. The Space then renames that default "Underway" (on the fake, as
    /// another Admin would); after the editor's next write - an add of "Blocked" - the "Add back" list names "Underway" and no
    /// longer "Doing".
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "FR-27")]
    [Trait("Requirement", "FR-25")]
    public async Task The_add_back_list_takes_a_Space_rename_after_the_editors_next_write()
    {
        var api = TwoSpaces();
        Seed(api, Northwind, Atlas, "Atlas");
        Shape(api, Northwind, Atlas);

        api.ProjectStatuses[(Northwind, Atlas)].RemoveAt(1);

        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await api.InstallAsync(page);
            await page.GotoAsync(Address(ClientSettings(Northwind, Atlas)));

            var addBack = page.Locator(Editor).GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "Add back" });

            await addBack.First.WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

            Assert.Contains("Doing", await page.Locator(Editor).InnerTextAsync(), StringComparison.Ordinal);

            var defaults = api.SpaceDefaultStatuses[Northwind];

            defaults[1] = defaults[1] with { Name = "Underway" };

            await page.GetByRole(AriaRole.Textbox, new PageGetByRoleOptions { Name = "New Status name" }).FillAsync("Blocked");
            await page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Add a Status", Exact = true }).ClickAsync();
            await page.Locator(Editor).Locator(RowName, new LocatorLocatorOptions { HasText = "Blocked" }).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });
            await page.Locator(Editor).GetByText("Underway").First.WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

            var text = await page.Locator(Editor).InnerTextAsync();

            Assert.Contains("Underway", text, StringComparison.Ordinal);
            Assert.DoesNotContain("Doing", text, StringComparison.Ordinal);
            Assert.Equal(1, await addBack.CountAsync());
        }
    }
}
