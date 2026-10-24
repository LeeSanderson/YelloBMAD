using System.Net;
using System.Reflection;
using Microsoft.AspNetCore.Components;
using Mono.Cecil;
using Xunit;
using Yello.Client.Pages;
using Yello.Client.Sessions;
using Yello.Contracts.Accounts;

namespace Yello.Tests.Architecture;

/// <summary>
/// The client's half of AC3 and AC6: the purge seam, the order it runs in, and what a 401 does.
/// UX-DR30, UX-DR31, UX-DR35.
/// </summary>
/// <remarks>
/// <para>
/// <b>THE PURGE IS STILL NEARLY EMPTY, AND NOTHING HERE PRETENDS OTHERWISE.</b> Story 1.7 registered
/// the active Space's state and the switcher's list with it; there is still no replica, no cached
/// Board and no filter state. A test asserting "everything Space-scoped is purged" would pass against
/// a system with nothing to purge, which is the vacuous-gate defect this repository keeps
/// rediscovering. So what is asserted below is what the seam <i>does</i> - registrations run, they
/// run before the announcement, a failing one does not strand the rest - and not what it will one day
/// cover. Story 1.7's sign-out entry and the active Space's purge are asserted in
/// <c>ActiveSpaceStateTests</c>; epic 2 extends it with the Board.
/// </para>
/// <para>
/// <b>In the architecture suite because it is the only suite that can see <c>Yello.Client</c>.</b>
/// The ring table gives <c>Yello.Tests.Slices</c> no edge to it, and <c>Yello.Tests.Client</c> sees
/// only <c>Yello.Contracts</c> by design - a compile-time edge there would let a case assert
/// against a type instead of against what the browser received. Widening either row is an
/// architecture edit, and this story does not make one for a unit test.
/// </para>
/// </remarks>
[Trait("Suite", "Architecture")]
[Trait("Priority", "P0")]
[Trait("Requirement", "FR-2")]
[Trait("Requirement", "UX-DR30")]
[Trait("Requirement", "UX-DR31")]
[Trait("Requirement", "AR-14")]
public sealed class SessionSeamTests
{
    /// <summary>
    /// UX-DR30: everything registered is purged, and all of it before anything is announced.
    /// </summary>
    /// <remarks>
    /// <b>Reversing the order renders a queued frame one tick after the announcement, disclosing a
    /// Space the Account no longer belongs to.</b> That is the entire content of UX-DR30, and it is
    /// the kind of rule that survives as a comment until somebody refactors. Asserted as a
    /// <i>sequence</i> rather than as two independent facts, because "both happened" is true of the
    /// wrong order too.
    /// </remarks>
    [Fact]
    public async Task Every_purge_runs_before_the_session_end_is_announced()
    {
        var order = new List<string>();
        var session = new SessionState();

        session.RegisterPurge(() =>
        {
            order.Add("purge-board");
            return Task.CompletedTask;
        });

        session.RegisterPurge(() =>
        {
            order.Add("purge-filters");
            return Task.CompletedTask;
        });

        session.AnnounceSessionEndWith(() =>
        {
            order.Add("announce");
            return Task.CompletedTask;
        });

        await session.SessionEndedAsync();

        Assert.Equal(["purge-board", "purge-filters", "announce"], order);
    }

    /// <summary>
    /// A purge that throws does not abandon the purges after it, nor the announcement.
    /// </summary>
    /// <remarks>
    /// A half-purged client left where it was is strictly worse than either outcome: it is the
    /// state UX-DR30 describes, with Space-scoped content still on screen and nobody told. The
    /// realistic cause is a component torn down while its own purge is running.
    /// </remarks>
    [Fact]
    public async Task A_purge_that_fails_does_not_strand_the_rest()
    {
        var order = new List<string>();
        var session = new SessionState();

        session.RegisterPurge(() => throw new InvalidOperationException("torn down mid-purge"));
        session.RegisterPurge(() =>
        {
            order.Add("purge-after-the-failure");
            return Task.CompletedTask;
        });

        session.AnnounceSessionEndWith(() =>
        {
            order.Add("announce");
            return Task.CompletedTask;
        });

        await session.SessionEndedAsync();

        Assert.Equal(["purge-after-the-failure", "announce"], order);
    }

    /// <summary>
    /// A component that unregisters does not leave a callback reaching into something that is gone.
    /// </summary>
    [Fact]
    public async Task An_unregistered_purge_does_not_run()
    {
        var ran = false;
        var session = new SessionState();

        var registration = session.RegisterPurge(() =>
        {
            ran = true;
            return Task.CompletedTask;
        });

        registration.Dispose();

        await session.SessionEndedAsync();

        Assert.False(ran);
    }

    /// <summary>
    /// Ending a Session forgets that there was one, and forgets the anti-forgery token with it.
    /// </summary>
    /// <remarks>
    /// The flag is what tells a later 401 apart from "was never signed in", so leaving it set would
    /// announce "your Session ended" to somebody who had never had one. The token is dropped
    /// because the server's paired cookie is gone with the Session, and holding half of a pair is
    /// worse than holding neither.
    /// </remarks>
    [Fact]
    public async Task Ending_a_session_forgets_the_session_and_its_token()
    {
        var session = new SessionState();

        session.SignedIn();
        session.HoldAntiforgeryToken("a-request-token");

        Assert.True(session.IsSignedIn);

        await session.SessionEndedAsync();

        Assert.False(session.IsSignedIn);
        Assert.Null(session.AntiforgeryRequestToken);
    }

    /// <summary>
    /// Signing in drops the anti-forgery token the sign-in itself used.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>ASP.NET Core binds an anti-forgery token to the identity it was issued to.</b> The token
    /// the sign-in request carried was minted while anonymous, so it is <i>refused</i> on the next
    /// request now that a Session exists - and the first state-changing thing a person does after
    /// signing in would fail with a 400 that explains nothing.
    /// </para>
    /// <para>
    /// <b>Found by <c>SessionEndpointTests</c> on 2026-09-18 rather than reasoned about in
    /// advance</b>: three sign-ins in one Host process produced one processed log line instead of
    /// three, because the second and third never reached the endpoint. This is the regression test
    /// for it, and it is here rather than only over the wire because the rule belongs to this class.
    /// </para>
    /// </remarks>
    [Fact]
    public void Signing_in_drops_the_token_that_was_issued_to_an_anonymous_caller()
    {
        var session = new SessionState();

        session.HoldAntiforgeryToken("a-token-minted-while-anonymous");
        session.SignedIn();

        Assert.True(session.IsSignedIn);
        Assert.Null(session.AntiforgeryRequestToken);
    }

    /// <summary>
    /// AC5 and AR-14: every request carries browser credentials, and every state-changing one
    /// carries the anti-forgery token.
    /// </summary>
    /// <remarks>
    /// <b>Credentials on every request, not on the authenticated ones.</b> <c>fetch</c> defaults to
    /// <c>credentials: "same-origin"</c>, and the client and the API are distinct origins - so
    /// without this the Session cookie is never sent and every authenticated request is anonymous,
    /// silently. Deciding per route would be a list a later story adds a route to and forgets.
    /// </remarks>
    [Fact]
    public async Task Every_request_includes_credentials_and_state_changing_ones_carry_the_token()
    {
        var session = new SessionState();

        session.SignedIn();
        session.HoldAntiforgeryToken("a-request-token");

        var (post, _) = await DriveAsync(session, HttpMethod.Post, "/api/v1/projects", HttpStatusCode.OK);
        var (get, _) = await DriveAsync(session, HttpMethod.Get, "/api/v1/projects", HttpStatusCode.OK);

        // The option the WebAssembly HTTP handler reads, asserted by its VALUE rather than by its
        // presence: `credentials: "same-origin"` is the browser default and is also an option that
        // is set, so "an option exists" would pass over the exact setting that sends no cookie.
        Assert.Equal("include", FetchCredentials(post));
        Assert.Equal("include", FetchCredentials(get));

        Assert.Equal("a-request-token", post.Headers.GetValues(Antiforgery.HeaderName).Single());

        // A GET changes no state, so AR-14 asks for no token on it - and sending one anyway would
        // put the value on requests that do not need it.
        Assert.False(get.Headers.Contains(Antiforgery.HeaderName));
    }

    /// <summary>
    /// AC3: a 401 from anywhere but sign-in purges and returns the User to sign-in.
    /// </summary>
    [Fact]
    public async Task A_401_ends_the_session_and_announces_it()
    {
        var order = new List<string>();
        var session = new SessionState();

        session.SignedIn();
        session.HoldAntiforgeryToken("a-request-token");
        session.RegisterPurge(() =>
        {
            order.Add("purge");
            return Task.CompletedTask;
        });

        session.AnnounceSessionEndWith(() =>
        {
            order.Add("announce");
            return Task.CompletedTask;
        });

        await DriveAsync(session, HttpMethod.Get, "/api/v1/sessions/current", HttpStatusCode.Unauthorized);

        Assert.Equal(["purge", "announce"], order);
        Assert.False(session.IsSignedIn);
    }

    /// <summary>
    /// A 401 from sign-in itself is a wrong password, not a Session that ended.
    /// </summary>
    /// <remarks>
    /// <b>This is the case that would otherwise fire in the middle of somebody typing their
    /// password wrongly</b>, purging and navigating and announcing an event that did not happen.
    /// The sign-in endpoint answers 401 for every failure cause by design (AD-23), so the handler
    /// has to exclude it explicitly rather than relying on the status code alone.
    /// </remarks>
    [Fact]
    public async Task A_401_from_sign_in_itself_is_not_a_session_ending()
    {
        var announced = false;
        var session = new SessionState();

        session.SignedIn();
        session.HoldAntiforgeryToken("a-request-token");
        session.AnnounceSessionEndWith(() =>
        {
            announced = true;
            return Task.CompletedTask;
        });

        await DriveAsync(session, HttpMethod.Post, SessionRoutes.Authenticate, HttpStatusCode.Unauthorized);

        Assert.False(announced);
        Assert.True(session.IsSignedIn);
    }

    /// <summary>
    /// A 401 to a client that was never signed in announces nothing either.
    /// </summary>
    /// <remarks>
    /// "Your Session ended" is false for somebody who never had one, and UX-DR35's whole content is
    /// that the reason stated must be the reason.
    /// </remarks>
    [Fact]
    public async Task A_401_without_a_session_announces_nothing()
    {
        var announced = false;
        var session = new SessionState();

        session.AnnounceSessionEndWith(() =>
        {
            announced = true;
            return Task.CompletedTask;
        });

        await DriveAsync(session, HttpMethod.Get, "/api/v1/sessions/current", HttpStatusCode.Unauthorized);

        Assert.False(announced);
    }

    /// <summary>
    /// AC6: nothing in the client re-sends a request after a Session ends.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>What this asserts, and what it cannot.</b> AC6 is about locally-held unsent input being
    /// replayed under a new Session, and there is no editor and no unsent input in this product
    /// until epic 7 - so the clause describes behaviour over a system that does not exist. What can
    /// be asserted honestly now is the rule and the absence of any code path that would break it:
    /// the failed request is returned to its caller as a failure, the handler retries nothing, and
    /// no request is stored anywhere to be re-sent.
    /// </para>
    /// <para>
    /// Asserted as a send count, because a retry is the shape this would actually take: a handler
    /// that re-authenticated and replayed would show two.
    /// </para>
    /// </remarks>
    [Fact]
    public async Task A_refused_request_is_never_retried_after_the_session_ends()
    {
        var session = new SessionState();

        session.SignedIn();
        session.HoldAntiforgeryToken("a-request-token");
        session.AnnounceSessionEndWith(() => Task.CompletedTask);

        var (_, sends) = await DriveAsync(
            session,
            HttpMethod.Post,
            "/api/v1/projects",
            HttpStatusCode.Unauthorized);

        Assert.Equal(1, sends);
    }

    /// <summary>
    /// The query key the client writes and the parameter the surface reads are the same key.
    /// </summary>
    /// <remarks>
    /// Blazor binds <c>?reason=</c> to a <c>[SupplyParameterFromQuery]</c> property by the
    /// property's name, while <c>ClientRoutes.SignInAfterSessionEnd</c> builds the address from
    /// <c>ReasonQueryKey</c>. Two spellings of one key, and a rename of either is silent: the
    /// person arrives at sign-in with no reason stated, which is exactly the half of AC3 that would
    /// be hardest to notice.
    /// </remarks>
    [Fact]
    public void The_reason_query_key_matches_the_parameter_that_reads_it()
    {
        var parameter = typeof(SignIn)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .SingleOrDefault(property =>
                Attribute.IsDefined(property, typeof(SupplyParameterFromQueryAttribute)));

        Assert.NotNull(parameter);

        var declaredName = parameter.GetCustomAttribute<SupplyParameterFromQueryAttribute>()!.Name
            ?? parameter.Name;

        Assert.Equal(ClientRoutes.ReasonQueryKey, declaredName, ignoreCase: true);

        // And the address the client actually navigates to carries that key and that value.
        Assert.Contains(
            $"{ClientRoutes.ReasonQueryKey}={ClientRoutes.SessionEndedReason}",
            ClientRoutes.SignInAfterSessionEnd,
            StringComparison.Ordinal);
    }

    /// <summary>
    /// The client's composition root actually wires the announcement, so the seam is not inert.
    /// </summary>
    /// <remarks>
    /// <b>Read from compiled IL rather than from source text</b>, following
    /// <c>LocalisationGateTests.The_client_calls_the_document_language_interop</c>: a source scan
    /// would be satisfied by the method name appearing in a comment, and this repository's comments
    /// mention it in several places. Without a wired announcement, every purge above would run and
    /// nobody would ever be returned to sign-in - a seam that works and does nothing.
    /// </remarks>
    [Fact]
    public void The_client_wires_what_happens_when_a_session_ends()
    {
        var clientAssembly = ProductionAssemblies.All
            .Single(assembly => assembly.GetName().Name == "Yello.Client");

        using var module = ModuleDefinition.ReadModule(clientAssembly.Location);

        var callers = (
            from type in module.GetTypes()
            from method in type.Methods
            where method.HasBody
            where method.Body.Instructions.Any(instruction =>
                instruction.Operand is MethodReference called
                && called.Name.Equals(nameof(SessionState.AnnounceSessionEndWith), StringComparison.Ordinal))
            select $"{type.FullName}.{method.Name}")
            .ToList();

        Assert.True(
            callers.Count > 0,
            $"Nothing in Yello.Client calls {nameof(SessionState)}."
            + $"{nameof(SessionState.AnnounceSessionEndWith)}, so a Session that ends purges "
            + "whatever is registered and then leaves the person exactly where they were, with no "
            + "reason stated. AC3 requires the User to be returned to sign-in with the reason "
            + "stated, and the purge seam without the announcement is half of it.");
    }

    /// <summary>
    /// The <c>credentials</c> value the WebAssembly handler would pass to <c>fetch</c>.
    /// </summary>
    /// <remarks>
    /// <c>SetBrowserRequestCredentials</c> writes into an options bag the WebAssembly handler reads
    /// at send time, so outside a browser the only evidence it ran is the bag itself. Read by the
    /// documented key rather than reflected over, and asserted as <c>"include"</c> - which is the
    /// value, not merely the presence.
    /// </remarks>
    private static string? FetchCredentials(HttpRequestMessage request) =>
        request.Options.TryGetValue(
            new HttpRequestOptionsKey<IDictionary<string, object>>("WebAssemblyFetchOptions"),
            out var fetchOptions)
            && fetchOptions.TryGetValue("credentials", out var credentials)
            ? credentials as string
            : null;

    /// <summary>
    /// Drives the handler with a stubbed transport and reports the request it produced.
    /// </summary>
    private static async Task<(HttpRequestMessage Request, int Sends)> DriveAsync(
        SessionState session,
        HttpMethod method,
        string path,
        HttpStatusCode status)
    {
        var inner = new RecordingHandler(status);

        using var handler = new YelloApiHandler(session) { InnerHandler = inner };
        using var client = new HttpClient(handler) { BaseAddress = new Uri("https://host.yello.test") };
        using var request = new HttpRequestMessage(method, new Uri(path, UriKind.Relative));

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        return (inner.LastRequest!, inner.Sends);
    }

    private sealed class RecordingHandler(HttpStatusCode status) : HttpMessageHandler
    {
        public int Sends { get; private set; }

        public HttpRequestMessage? LastRequest { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken token)
        {
            Sends++;
            LastRequest = request;

            return Task.FromResult(new HttpResponseMessage(status));
        }
    }
}
