using System.Net;
using Xunit;
using Yello.Contracts.Accounts;
using Yello.Tests.Shared;
using Yello.Tests.Slices.Accounts.RegisterAccount;

namespace Yello.Tests.Slices.Deployment;

/// <summary>
/// The Host behind the platform's proxy: it sees the original scheme and the client's address from
/// the hop the platform appends, never from a value a client can rotate, and the credential-hashing
/// rate limit partitions on that address, normalised. Story 1.10 AC11 (AD-7, AR-25 / AD-18);
/// Task 4, Q10 (a), Finding F3; <c>deferred-work.md:171, 290, 304</c>.
/// </summary>
/// <remarks>
/// <para>
/// <b>The Host reads forwarded headers only when configured</b> (<c>ForwardedHeadersPolicy</c>).
/// </para>
/// <para>
/// <b>Plain http, as behind ingress</b> (<see cref="DeploymentHosts.BehindIngress"/>). The scheme case
/// is the one that decides whether the deployed product can sign anyone in at all: with
/// <c>CookieSecurePolicy.Always</c> the anti-forgery system refuses to issue a token over a non-SSL
/// request (<c>TESTING-CONVENTIONS.md</c>, "The Host process is served over HTTPS").
/// </para>
/// <para>
/// <b>The rate-limit cases</b> lower <c>RateLimiting__PermitsPerWindow</c> to 3 and send sign-ins for
/// an unknown address (each one a <c>401</c> that reads the database). Each case starts its own Host,
/// so no window is shared between cases. Every "shares a window" assertion is anchored on an address
/// that does not share it, so a limiter that counted every request in one partition fails.
/// </para>
/// </remarks>
/// <param name="fixture">This class's migrated database on the shared engine.</param>
[Trait("Suite", "Slices")]
[Trait("Priority", "P0")]
[Trait("Requirement", "AD-7")]
[Trait("Requirement", "AR-25")]
[Trait("Requirement", "AD-18")]
public sealed class ForwardedHeadersTests(MigratedDatabaseFixture fixture) : IClassFixture<MigratedDatabaseFixture>
{
    private const int Permits = 3;

    /// <summary>
    /// 1.10-SLC-005 - with forwarding on, the anti-forgery token is issued over http when the platform
    /// forwards <c>https</c>, and its cookie is <c>Secure</c>. Anchor: the same Host refuses it without
    /// the header.
    /// </summary>
    [Fact]
    public async Task Behind_ingress_the_forwarded_scheme_lets_the_host_issue_an_antiforgery_token()
    {
        var connectionString = Available();

        HttpStatusCode? plain = null;
        HttpStatusCode? forwarded = null;
        string? cookie = null;

        await HostProcess.RunAsync(connectionString, DeploymentHosts.BehindIngress(forwarding: true), async client =>
        {
            using (var first = await client.GetAsync(new Uri(SessionRoutes.AntiforgeryToken, UriKind.Relative)))
            {
                plain = first.StatusCode;
            }

            DeploymentHosts.ForwardHttps(client);
            using var response = await client.GetAsync(new Uri(SessionRoutes.AntiforgeryToken, UriKind.Relative));
            forwarded = response.StatusCode;
            cookie = response.Headers.TryGetValues("Set-Cookie", out var values) ? string.Join('\n', values) : null;
        });

        Assert.NotEqual(HttpStatusCode.OK, plain);
        Assert.Equal(HttpStatusCode.OK, forwarded);
        Assert.NotNull(cookie);
        Assert.Contains("secure", cookie, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 1.10-SLC-006 - with forwarding off (a local Host), <c>X-Forwarded-Proto</c> is ignored. Anchor:
    /// the same request is honoured with forwarding on.
    /// </summary>
    [Fact]
    public async Task With_forwarding_off_a_forwarded_scheme_is_ignored()
    {
        var connectionString = Available();

        HttpStatusCode? off = null;
        HttpStatusCode? on = null;

        await HostProcess.RunAsync(connectionString, DeploymentHosts.BehindIngress(forwarding: false), async client =>
        {
            DeploymentHosts.ForwardHttps(client);
            using var response = await client.GetAsync(new Uri(SessionRoutes.AntiforgeryToken, UriKind.Relative));
            off = response.StatusCode;
        });

        await HostProcess.RunAsync(connectionString, DeploymentHosts.BehindIngress(forwarding: true), async client =>
        {
            DeploymentHosts.ForwardHttps(client);
            using var response = await client.GetAsync(new Uri(SessionRoutes.AntiforgeryToken, UriKind.Relative));
            on = response.StatusCode;
        });

        Assert.NotEqual(HttpStatusCode.OK, off);
        Assert.Equal(HttpStatusCode.OK, on);
    }

    /// <summary>
    /// 1.10-SLC-007 - a client that rotates the left-hand part of <c>X-Forwarded-For</c> still shares
    /// one window with every other request the platform forwarded from the same address. Anchor: a
    /// different platform hop is not refused.
    /// </summary>
    [Fact]
    public async Task A_rotated_left_hand_forwarded_address_does_not_escape_the_window()
    {
        var verdict = await BurstAsync(
            exhaustFrom: () => $"198.51.100.{Random.Shared.Next(1, 254)}, 203.0.113.7",
            thenFrom: $"192.0.2.{Random.Shared.Next(1, 254)}, 203.0.113.7",
            anchorFrom: "198.51.100.1, 203.0.113.8");

        verdict.AssertShared();
    }

    /// <summary>
    /// 1.10-SLC-008 - an IPv4 address and its IPv4-mapped IPv6 form share one window.
    /// </summary>
    [Fact]
    [Trait("Priority", "P1")]
    public async Task An_ipv4_address_and_its_mapped_ipv6_form_share_one_window()
    {
        var verdict = await BurstAsync(
            exhaustFrom: () => "203.0.113.9",
            thenFrom: "::ffff:203.0.113.9",
            anchorFrom: "203.0.113.10");

        verdict.AssertShared();
    }

    /// <summary>
    /// 1.10-SLC-009 - IPv6 addresses in one /64 share one window; another /64 does not.
    /// </summary>
    [Fact]
    [Trait("Priority", "P1")]
    public async Task Ipv6_addresses_share_a_window_per_64_bit_prefix()
    {
        var verdict = await BurstAsync(
            exhaustFrom: () => "2001:db8:1:2::1",
            thenFrom: "2001:db8:1:2::2",
            anchorFrom: "2001:db8:1:3::1");

        verdict.AssertShared();
    }

    /// <summary>
    /// Exhausts the window from one forwarded address, then sends one sign-in from an address that
    /// should share it and one from an address that should not.
    /// </summary>
    private async Task<BurstVerdict> BurstAsync(Func<string> exhaustFrom, string thenFrom, string anchorFrom)
    {
        var connectionString = Available();
        var settings = DeploymentHosts.BehindIngress(forwarding: true);
        settings["RateLimiting__PermitsPerWindow"] = Permits.ToString(System.Globalization.CultureInfo.InvariantCulture);

        var exhausting = new List<HttpStatusCode>();
        HttpStatusCode? shared = null;
        HttpStatusCode? anchor = null;

        await HostProcess.RunAsync(connectionString, settings, async client =>
        {
            DeploymentHosts.ForwardHttps(client);

            var session = new WireSession(client);
            await session.FetchAntiforgeryTokenAsync();

            for (var attempt = 0; attempt < Permits; attempt++)
            {
                exhausting.Add(await SignInFromAsync(client, session, exhaustFrom()));
            }

            shared = await SignInFromAsync(client, session, thenFrom);
            anchor = await SignInFromAsync(client, session, anchorFrom);
        });

        return new BurstVerdict(exhausting, shared, anchor);
    }

    private static async Task<HttpStatusCode> SignInFromAsync(HttpClient client, WireSession session, string forwardedFor)
    {
        client.DefaultRequestHeaders.Remove(DeploymentHosts.ForwardedFor);
        client.DefaultRequestHeaders.TryAddWithoutValidation(DeploymentHosts.ForwardedFor, forwardedFor);

        return (await session.SignInAsync(DeploymentHosts.RandomAddress(), DeploymentHosts.Password)).StatusCode;
    }

    private string Available()
    {
        Assert.SkipUnless(fixture.IsAvailable, fixture.UnavailableReason ?? string.Empty);

        return fixture.ConnectionString!;
    }

    /// <summary>
    /// What a burst saw: the requests that exhausted the window, the one that should share it, and the
    /// anchor that should not.
    /// </summary>
    private sealed record BurstVerdict(IReadOnlyList<HttpStatusCode> Exhausting, HttpStatusCode? Shared, HttpStatusCode? Anchor)
    {
        public void AssertShared()
        {
            Assert.Equal(Permits, Exhausting.Count);
            Assert.All(Exhausting, status => Assert.Equal(HttpStatusCode.Unauthorized, status));
            Assert.Equal(HttpStatusCode.TooManyRequests, Shared);
            Assert.Equal(HttpStatusCode.Unauthorized, Anchor);
        }
    }
}
