using System.Globalization;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using Xunit;

namespace Yello.Tests.Architecture;

/// <summary>
/// Story 4.3's client gates for the Invitation view (AC2, AC13; UX-DR18, <c>DESIGN.md:270-276</c>, <c>:475</c>; ledger
/// <c>:36</c>): the <c>invitation-view</c> component's 6px radius and the most generous padding in the product; the host
/// page's fixed title and its lack of preview metadata; the static host's <c>X-Robots-Tag: noindex</c> and
/// <c>Referrer-Policy: no-referrer</c> for <c>/invitations/*</c>; the client route agreeing with the Host's link path and
/// reached without a Session; and the page's own head. Written by the story 4.3 ATDD run, 2026-11-01.
/// </summary>
/// <remarks>
/// <para>
/// <b>Written as red-phase scaffolds, activated by the dev phase.</b> The names are the story's: the component class
/// <c>.invitation-view</c>, <c>Yello.Client/Pages/InvitationView.razor</c>, <c>ClientRoutes.Invitation(string)</c>, and
/// <c>InvitationLinks.Path</c>.
/// </para>
/// <para>
/// <b>Left active and green, by design (a standing regression gate):</b>
/// <see cref="The_host_page_has_a_fixed_title_and_no_preview_metadata"/>, whose detector is proved by
/// <see cref="The_preview_metadata_scan_detects_a_plant"/>. <c>index.html</c> already holds it; 4.3 must not change that.
/// </para>
/// </remarks>
public sealed partial class DesignFoundationGateTests
{
    private const string InvitationViewClass = ".invitation-view";

    private const double InvitationViewRadiusPx = 6;

    private static readonly FileInfo HostPage = new(Path.Combine(RepositoryLayout.Root.FullName, "Yello.Client", "wwwroot", "index.html"));

    private static readonly FileInfo StaticHostConfiguration = new(Path.Combine(RepositoryLayout.Root.FullName, "Yello.Client", "wwwroot", "staticwebapp.config.json"));

    private static readonly FileInfo InvitationPage = new(Path.Combine(RepositoryLayout.Root.FullName, "Yello.Client", "Pages", "InvitationView.razor"));

    /// <summary>
    /// AC2, UX-DR18, <c>DESIGN.md:270-276</c>, ledger <c>:36</c> - every radius declared on the Invitation view resolves to
    /// 6px (<c>rounded.lg</c>), and one is declared. Closes the invitation-view clause of ledger <c>:36</c>.
    /// </summary>
    [Fact]
    [Trait("Requirement", "UX-DR18")]
    [Trait("Requirement", "UX-DR5")]
    public void The_invitation_view_takes_the_6px_radius() =>
        AssertRadius(InvitationViewClass, InvitationViewRadiusPx, "var(--radius-lg)");

    /// <summary>
    /// AC2, <c>DESIGN.md:475</c> ("the most generous padding in the product") - no rule in the stylesheets declares a
    /// padding, in any direction, larger than the largest the Invitation view declares; and the view declares one.
    /// </summary>
    [Fact]
    [Trait("Requirement", "UX-DR18")]
    public void No_component_declares_more_padding_than_the_invitation_view()
    {
        var paddings = CssCorpus.AllDeclarations()
            .Where(d => d.Declaration.Property.StartsWith("padding", StringComparison.OrdinalIgnoreCase))
            .ToList();

        var invitation = paddings
            .Where(d => SelectsComponent(d.Rule.Selector, InvitationViewClass))
            .SelectMany(d => CssCorpus.PixelLengths(CssCorpus.Resolve(d.Declaration.Value)))
            .ToList();

        Assert.True(invitation.Count > 0, $"No rule gives {InvitationViewClass} a padding.");

        var most = invitation.Max();

        // Corrected by the dev phase (story 4.3): two rules pad more than spacing.6 and are not components. The page
        // shell the registration panel and this view sit in (.auth-surface) spaces them from the viewport's top edge; the
        // Board's cold-load skeleton (.board-column--skeleton) holds no text and is given its depth by spacing alone. The
        // comparison DESIGN.md:475 makes is between components, so they are named here, each with its reason, rather than
        // the view taking spacing.7, which DESIGN.md:275 does not give it.
        string[] notComponents = [".auth-surface", ".board-column--skeleton"];

        var problems = paddings
            .Where(d => !SelectsComponent(d.Rule.Selector, InvitationViewClass))
            .Where(d => !Array.Exists(notComponents, layout => SelectsComponent(d.Rule.Selector, layout)))
            .SelectMany(d => CssCorpus.PixelLengths(CssCorpus.Resolve(d.Declaration.Value))
                .Where(px => px > most + LengthTolerance)
                .Select(px => $"{Where(d.Sheet, d.Declaration)} declares {px.ToString(CultureInfo.InvariantCulture)}px of padding on {d.Rule.Selector}."))
            .ToList();

        AssertNoProblems(problems, $"A component is padded more generously than {InvitationViewClass} ({most.ToString(CultureInfo.InvariantCulture)}px).", "Give the Invitation view the largest padding token (spacing.6), and nothing else more.");
    }

    /// <summary>
    /// AC2, UX-DR18, <c>EXPERIENCE.md:177</c> - the host page every client route is served from (the Invitation link's
    /// included) has the fixed <c>&lt;title&gt;Yello&lt;/title&gt;</c> and no <c>og:</c> or <c>twitter:</c> metadata, so a
    /// link pasted into a chat tool previews no Space. Standing gate: green today.
    /// </summary>
    [Fact]
    [Trait("Requirement", "UX-DR18")]
    [Trait("Requirement", "FR-11")]
    public void The_host_page_has_a_fixed_title_and_no_preview_metadata()
    {
        Assert.True(HostPage.Exists, $"{HostPage.FullName} is missing.");

        var problems = PreviewMetadataProblems(File.ReadAllText(HostPage.FullName));

        Assert.True(problems.Count == 0, string.Join('\n', problems));
    }

    /// <summary>
    /// The detector behind <see cref="The_host_page_has_a_fixed_title_and_no_preview_metadata"/>, fired on planted pages.
    /// </summary>
    [Fact]
    public void The_preview_metadata_scan_detects_a_plant()
    {
        Assert.Empty(PreviewMetadataProblems("<head><title>Yello</title></head>"));
        Assert.NotEmpty(PreviewMetadataProblems("<head><title>Yello</title><meta property=\"og:title\" content=\"Northwind\" /></head>"));
        Assert.NotEmpty(PreviewMetadataProblems("<head><title>Yello</title><meta name=\"twitter:card\" content=\"summary\" /></head>"));
        Assert.NotEmpty(PreviewMetadataProblems("<head><title>Northwind - Yello</title></head>"));
        Assert.NotEmpty(PreviewMetadataProblems("<head></head>"));
    }

    /// <summary>
    /// AC2, UX-DR18 - <c>noindex</c> must reach a scraper that runs no WebAssembly, and the page URL carries the token, so
    /// the static host's configuration answers <c>/invitations/*</c> with <c>X-Robots-Tag: noindex</c> and
    /// <c>Referrer-Policy: no-referrer</c> (a route-level <c>headers</c> entry). Plant 15: the noindex header removed.
    /// </summary>
    [Fact]
    [Trait("Requirement", "UX-DR18")]
    [Trait("Requirement", "NFR-6")]
    public void The_static_host_sends_noindex_and_no_referrer_for_invitation_pages()
    {
        using var configuration = JsonDocument.Parse(File.ReadAllText(StaticHostConfiguration.FullName));

        Assert.True(configuration.RootElement.TryGetProperty("routes", out var routes), "staticwebapp.config.json declares no routes.");

        var invitation = Assert.Single(routes.EnumerateArray(), route => route.TryGetProperty("route", out var pattern) && pattern.GetString() == "/invitations/*");

        Assert.True(invitation.TryGetProperty("headers", out var headers), "The /invitations/* route declares no headers.");

        var declared = headers.EnumerateObject().ToDictionary(header => header.Name, header => header.Value.GetString(), StringComparer.OrdinalIgnoreCase);

        Assert.Equal("noindex", declared.GetValueOrDefault("X-Robots-Tag"));
        Assert.Equal("no-referrer", declared.GetValueOrDefault("Referrer-Policy"));
    }

    /// <summary>
    /// AC2, AC13 - the client's Invitation route is the Host's link path plus the token (the email's link opens the page the
    /// client routes), and it is reached without a Session, so a link holder is not bounced to sign-in. Anchor: a Space route
    /// still needs one.
    /// </summary>
    [Fact]
    [Trait("Requirement", "UX-DR18")]
    [Trait("Requirement", "FR-11")]
    public void The_client_invitation_route_is_the_Hosts_link_path_and_is_reached_without_a_Session()
    {
        var routes = Assert.Single(ProductionAssemblies.Client.GetTypes(), type => type.FullName == "Yello.Client.Pages.ClientRoutes");
        var invitation = routes.GetMethod("Invitation", BindingFlags.Public | BindingFlags.Static, [typeof(string)]);
        var reached = routes.GetMethod("IsReachedWithoutASession", BindingFlags.Public | BindingFlags.Static);
        var links = Assert.Single(ProductionAssemblies.Application.GetTypes(), type => type.FullName == "Yello.Application.Invitations.InvitationLinks");
        var linkPath = Assert.IsType<string>(links.GetProperty("Path", BindingFlags.Public | BindingFlags.Static)?.GetValue(null));

        Assert.True(invitation is not null, "ClientRoutes.Invitation(string) is not declared.");
        Assert.True(reached is not null, "ClientRoutes.IsReachedWithoutASession is not declared.");

        const string token = "yello_inv_AbC-dEf_123";

        var path = Assert.IsType<string>(invitation.Invoke(null, [token]));

        Assert.Equal(linkPath + token, path);
        Assert.True(reached.Invoke(null, [path.TrimStart('/')]) is true, $"{path} is not reached without a Session.");
        Assert.True(reached.Invoke(null, [$"spaces/{Guid.NewGuid():D}"]) is false, "A Space route is reached without a Session: the predicate is vacuous.");
    }

    /// <summary>
    /// AC2, UX-DR18, NFR-9 - the Invitation page is routed at <c>/invitations/{Token}</c>, sets no <c>&lt;PageTitle&gt;</c> -
    /// so the title stays the host page's fixed <c>Yello</c>, as on every page - and adds
    /// <c>&lt;meta name="robots" content="noindex"&gt;</c> through <c>HeadContent</c> for the browsers that do run it.
    /// </summary>
    /// <remarks>
    /// <b>Corrected by the dev phase (story 4.3).</b> The scaffold required a <c>PageTitle</c> of <c>Yello</c>; story 1.7's
    /// standing gate <c>SpaceSwitcherGateTests.No_client_code_sets_the_document_title</c> forbids any client title setter
    /// (UX-DR33), and the host page's title is already exactly <c>Yello</c>. One source for the title is the stronger
    /// guarantee, so this asserts its absence; that gate now admits the robots meta alone in a <c>HeadContent</c>.
    /// </remarks>
    [Fact]
    [Trait("Requirement", "UX-DR18")]
    public void The_invitation_page_has_a_fixed_title_and_a_noindex_meta()
    {
        Assert.True(InvitationPage.Exists, $"{InvitationPage.FullName} is missing.");

        var page = File.ReadAllText(InvitationPage.FullName);

        Assert.Contains("@page \"/invitations/{Token}\"", page, StringComparison.Ordinal);
        Assert.False(PageTitleContent.IsMatch(page), "The Invitation page sets a PageTitle: the title is the host page's fixed 'Yello'.");
        Assert.Matches(RobotsNoindex, page);
    }

    [GeneratedRegex(@"<PageTitle>(?<content>.*?)</PageTitle>", RegexOptions.Singleline, 1000)]
    private static partial Regex PageTitleContent { get; }

    [GeneratedRegex(@"<HeadContent>.*<meta\s+name=""robots""\s+content=""noindex""\s*/?>.*</HeadContent>", RegexOptions.Singleline, 1000)]
    private static partial Regex RobotsNoindex { get; }

    [GeneratedRegex(@"<title>(?<title>.*?)</title>", RegexOptions.Singleline | RegexOptions.IgnoreCase, 1000)]
    private static partial Regex HtmlTitle { get; }

    [GeneratedRegex(@"<meta\b[^>]*\b(property|name)\s*=\s*""(og|twitter):", RegexOptions.IgnoreCase, 1000)]
    private static partial Regex PreviewMeta { get; }

    /// <summary>
    /// What a page's head would disclose to a link previewer: a title other than exactly <c>Yello</c>, or any <c>og:</c> or
    /// <c>twitter:</c> meta.
    /// </summary>
    private static List<string> PreviewMetadataProblems(string html)
    {
        var problems = new List<string>();
        var title = HtmlTitle.Match(html);

        if (!title.Success || title.Groups["title"].Value.Trim() != "Yello")
        {
            problems.Add($"The title is not exactly 'Yello': '{(title.Success ? title.Groups["title"].Value : "(none)")}'.");
        }

        problems.AddRange(PreviewMeta.Matches(html).Select(match => $"Preview metadata: {match.Value}"));

        return problems;
    }
}
