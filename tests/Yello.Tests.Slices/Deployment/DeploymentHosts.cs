using System.Net;
using Microsoft.Data.SqlClient;
using Xunit;
using Yello.Contracts.Accounts;
using Yello.Tests.Shared;

namespace Yello.Tests.Slices.Deployment;

/// <summary>
/// What story 1.10's wire cases start a Host with: the production-only settings the IaC sets, a
/// database that cannot be reached, and a user profile of its own.
/// </summary>
/// <remarks>
/// <para>
/// <b>Written with the red-phase scaffolds by the ATDD run on 2026-10-25.</b> Nothing here is product
/// code; every setting is an environment value the Host already reads or that story 1.10 Task 4 adds
/// (<c>ForwardedHeaders__Enabled</c>).
/// </para>
/// <para>
/// <b>Behind ingress means plain http.</b> Container Apps ingress terminates TLS, so the Host sees
/// <c>http</c> and learns the scheme from <c>X-Forwarded-Proto</c> (Finding F3). These Hosts listen on
/// <c>http://127.0.0.1:0</c> and a case sends the header the platform would have appended. That is also
/// why the fresh-profile cases use it: pointing <c>HOME</c> at an empty folder hides the development
/// certificate on Linux, so an https Host would not start on a CI agent.
/// </para>
/// </remarks>
internal static class DeploymentHosts
{
    /// <summary>
    /// The allowed client origin every Host here starts with.
    /// </summary>
    public const string ClientOrigin = "https://client.yello.test";

    /// <summary>
    /// The password every Account these cases register uses. Not a secret: a test fixture value.
    /// </summary>
    public const string Password = "a-password-nobody-else-uses-1!";

    /// <summary>
    /// The header the platform's ingress appends with the original scheme.
    /// </summary>
    public const string ForwardedProto = "X-Forwarded-Proto";

    /// <summary>
    /// The header the platform's ingress appends the client's address to.
    /// </summary>
    public const string ForwardedFor = "X-Forwarded-For";

    /// <summary>
    /// The Session cookie's name.
    /// </summary>
    public const string SessionCookie = "yello_session";

    /// <summary>
    /// A connection string naming a server nothing listens on, so any connection attempt fails at
    /// once (connection refused) and a probe or a refusal can be told apart from a database answer.
    /// </summary>
    public static string UnreachableDatabase { get; } = new SqlConnectionStringBuilder
    {
        DataSource = "tcp:127.0.0.1,1",
        InitialCatalog = "yello",
        ConnectTimeout = 2,
        Encrypt = SqlConnectionEncryptOption.Optional,
    }.ConnectionString;

    /// <summary>
    /// The Host as the IaC configures it behind ingress: plain http, forwarded headers on or off.
    /// </summary>
    /// <param name="forwarding">The value of <c>ForwardedHeaders__Enabled</c>.</param>
    /// <returns>The settings, by environment-variable name.</returns>
    public static Dictionary<string, string> BehindIngress(bool forwarding) =>
        new(StringComparer.Ordinal)
        {
            ["ClientOrigin"] = ClientOrigin,
            ["ASPNETCORE_URLS"] = "http://127.0.0.1:0",
            ["ForwardedHeaders__Enabled"] = forwarding ? "true" : "false",
        };

    /// <summary>
    /// <see cref="BehindIngress"/> with forwarding on, plus a user profile of the process's own,
    /// empty - which is what a container gets on every start (Finding F1).
    /// </summary>
    /// <param name="profile">An empty directory the Host treats as its user profile.</param>
    /// <returns>The settings.</returns>
    /// <remarks>
    /// Without this, ASP.NET Core keeps its key ring in the developer's own profile
    /// (<c>%LOCALAPPDATA%\ASP.NET\DataProtection-Keys</c>, <c>~/.aspnet/DataProtection-Keys</c>), every
    /// local Host process shares it, and F1 is invisible.
    /// </remarks>
    public static Dictionary<string, string> InAFreshContainer(DirectoryInfo profile)
    {
        ArgumentNullException.ThrowIfNull(profile);

        var settings = BehindIngress(forwarding: true);

        settings["LOCALAPPDATA"] = profile.FullName;
        settings["APPDATA"] = profile.FullName;
        settings["USERPROFILE"] = profile.FullName;
        settings["HOME"] = profile.FullName;

        return settings;
    }

    /// <summary>
    /// A new, empty directory under the system temp folder, for <see cref="InAFreshContainer"/>.
    /// </summary>
    /// <returns>The directory.</returns>
    public static DirectoryInfo NewProfile() =>
        Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "yello-profile-" + Guid.NewGuid().ToString("N")));

    /// <summary>
    /// A client of a Host behind ingress: every request carries the scheme the platform would forward.
    /// </summary>
    /// <param name="client">The client the harness bound to the Host.</param>
    public static void ForwardHttps(HttpClient client)
    {
        ArgumentNullException.ThrowIfNull(client);

        client.DefaultRequestHeaders.Remove(ForwardedProto);
        client.DefaultRequestHeaders.TryAddWithoutValidation(ForwardedProto, "https");
    }

    /// <summary>
    /// A random address no other case uses (FR-1 makes an Account's address unique).
    /// </summary>
    /// <returns>The address.</returns>
    public static string RandomAddress() => $"person-{Guid.NewGuid():N}@example.test";

    /// <summary>
    /// Registers an Account through the endpoint and signs it in on <paramref name="session"/>, then
    /// fetches the anti-forgery token that belongs to the signed-in identity.
    /// </summary>
    /// <param name="session">The session to sign in on.</param>
    /// <param name="address">The Account's address.</param>
    /// <returns>A task.</returns>
    public static Task RegisterAndSignInAsync(WireSession session, string address)
    {
        ArgumentNullException.ThrowIfNull(session);

        return RegisterAndSignInCoreAsync(session, address);
    }

    private static async Task RegisterAndSignInCoreAsync(WireSession session, string address)
    {
        await session.FetchAntiforgeryTokenAsync();

        var registered = await session.RegisterAsync(
            new RegisterAccountRequest("Ravi-" + Guid.NewGuid().ToString("N"), address, Password));

        Assert.Equal(HttpStatusCode.NoContent, registered.StatusCode);

        await session.FetchAntiforgeryTokenAsync();
        Assert.Equal(HttpStatusCode.NoContent, (await session.SignInAsync(address, Password)).StatusCode);

        // An anti-forgery token is bound to the identity it was issued to (TESTING-CONVENTIONS.md).
        await session.FetchAntiforgeryTokenAsync();
    }
}
