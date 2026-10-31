using System.Net;
using System.Reflection;
using System.Text;
using System.Xml.Linq;
using Xunit;
using Yello.Client.Spaces;
using Yello.Contracts.Spaces;

namespace Yello.Tests.Architecture;

/// <summary>
/// Story 3.3's client-side gates that need no browser: <c>SpaceDirectory.Removed(id)</c> drops a deleted Space from the
/// held list and from the refused set, and edits the directory rather than forgetting it (AC12); and the copy Q5-Q8
/// recommend is externalised once each with a translator's comment that names the story (AC8, AC12, AC14, AC15). Written
/// by the story 3.3 ATDD run, 2026-10-31.
/// </summary>
/// <remarks>
/// <para>
/// <b>Red-phase scaffolds, skipped with <c>red: AC&lt;n&gt;</c>.</b> <c>Removed</c> does not exist yet, so it is found by
/// reflection (a public instance <c>void Removed(Guid)</c>); a typed call would not compile. The copy is matched by value,
/// so a key may be named as the dev phase likes; a changed sentence is a recorded correction to Q5-Q8, not a quiet edit.
/// </para>
/// <para>
/// <b>No container, no Host.</b> <c>SpaceDirectory</c> is constructed on an <see cref="HttpClient"/> whose handler answers
/// the switcher with a fixed list, the way <c>SessionSeamTests</c> drives the client's seams in this suite (the ring gives
/// no other suite an edge to <c>Yello.Client</c>; TESTING-CONVENTIONS).
/// </para>
/// </remarks>
[Trait("Suite", "Architecture")]
[Trait("Priority", "P0")]
[Trait("Requirement", "FR-7")]
public sealed class SpaceDeletionClientGateTests
{
    private static readonly FileInfo ClientCopy =
        new(Path.Combine(RepositoryLayout.Root.FullName, "Yello.Client", "Localisation", "ClientCopy.resx"));

    /// <summary>
    /// The sentences Q5 (the confirm), Q6 (the announcement), Q7 (the switcher with nothing to list) and Q8 (the
    /// all-refused page at <c>/</c>) recommend, each a whole resource value. The count sentences carry placeholders whose
    /// shape the dev phase chooses, so they are asserted in the browser, not here.
    /// </summary>
    private static readonly string[] SpaceDeletionCopy =
    [
        "Delete this Space",
        "Type the Space name to confirm",
        "Type the Space name exactly as shown.",
        "No one else has access to it.",
        "Space deleted.",
        "Your Spaces could not be listed.",
        "Choose a Space from the switcher above.",
    ];

    /// <summary>
    /// AC12, UX-DR32, Task 6 - after a deletion the client calls <c>SpaceDirectory.Removed(id)</c>: the row leaves
    /// <c>Current</c>, the other rows stay in the order served, and the id leaves the refused set too (3.2's set only
    /// excludes, and a deleted Space must not linger in it). It edits the directory; it does not forget it - the held list
    /// is not <c>null</c> afterwards. Anchored: the directory held both rows and had the id refused before.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC12 - SpaceDirectory has no Removed(Guid) yet (story 3.3 Task 6)")]
    [Trait("Requirement", "UX-DR32")]
    public async Task Removed_drops_the_Space_from_the_held_list_and_from_the_refused_set()
    {
        var (deleted, kept) = (Guid.NewGuid(), Guid.NewGuid());

        using var http = new HttpClient(new SwitcherAnswer([new(deleted, "Northwind Redesign"), new(kept, "Harbour Ops")]))
        {
            BaseAddress = new Uri("https://api.yello.test/"),
        };

        var directory = new SpaceDirectory(http);

        Assert.Equal(2, (await directory.RefreshAsync(TestContext.Current.CancellationToken))?.Count);

        directory.Refused(deleted);
        Assert.True(directory.RefusedInThisLoad(deleted));

        var removed = typeof(SpaceDirectory).GetMethod("Removed", BindingFlags.Public | BindingFlags.Instance, [typeof(Guid)]);

        Assert.True(removed is not null, "SpaceDirectory has no public void Removed(Guid).");

        removed.Invoke(directory, [deleted]);

        Assert.NotNull(directory.Current);
        Assert.Equal([kept], directory.Current.Select(space => space.Id));
        Assert.False(directory.Lists(deleted));
        Assert.False(directory.RefusedInThisLoad(deleted), "A deleted Space was left in the refused set.");
    }

    /// <summary>
    /// AC8, AC12, AC14, AC15, UX-DR36, UX-DR38, UX-DR42 - each sentence Q5-Q8 recommend is a <c>ClientCopy</c> value
    /// exactly once, with a translator's comment naming story 3.3 (Task 6: "Each entry carries a <c>&lt;comment&gt;</c>
    /// naming 3.3 and the AC"). "Delete {0}?" and "This cannot be undone." are 2.5's and are reused, never copied - held by
    /// <c>DeletionCopyGateTests</c>.
    /// </summary>
    [Fact(Skip = "red: AC8, AC12, AC14, AC15 - the Q5-Q8 copy is not in ClientCopy.resx yet (story 3.3 Task 6)")]
    [Trait("Requirement", "UX-DR36")]
    [Trait("Requirement", "UX-DR42")]
    public void The_Space_deletion_copy_is_externalised_once_each_with_a_comment_naming_the_story()
    {
        Assert.True(ClientCopy.Exists, $"{ClientCopy.FullName} does not exist.");

        var entries = XDocument.Load(ClientCopy.FullName).Root!.Elements("data")
            .Select(data => (Value: data.Element("value")?.Value ?? string.Empty, Comment: data.Element("comment")?.Value ?? string.Empty))
            .ToList();

        foreach (var copy in SpaceDeletionCopy)
        {
            var matches = entries.Where(entry => string.Equals(entry.Value, copy, StringComparison.Ordinal)).ToList();

            Assert.True(matches.Count == 1, $"\"{copy}\" is a ClientCopy value {matches.Count} times; expected exactly once.");
            Assert.Contains("3.3", matches[0].Comment, StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// Answers <c>GET /api/v1/spaces</c> with a fixed list, and anything else with <c>404</c>.
    /// </summary>
    private sealed class SwitcherAnswer(IReadOnlyList<SpaceIdentityResponse> spaces) : HttpMessageHandler
    {
        private static readonly System.Text.Json.JsonSerializerOptions JsonOptions = new(System.Text.Json.JsonSerializerDefaults.Web);

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.Method != HttpMethod.Get || request.RequestUri?.AbsolutePath != SpaceRoutes.Directory)
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
            }

            var body = System.Text.Json.JsonSerializer.Serialize(spaces, JsonOptions);

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json"),
            });
        }
    }
}
