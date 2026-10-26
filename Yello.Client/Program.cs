using System.Globalization;
using System.Net;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.JSInterop;
using Microsoft.AspNetCore.Components;
using Yello.Client;
using Yello.Client.Layout;
using Yello.Client.Localisation;
using Yello.Client.Pages;
using Yello.Client.Sessions;
using Yello.Client.Spaces;
using Yello.Contracts.Accounts;
using Yello.Contracts.Localisation;

// The client's composition root.
//
// Superseded 2026-08-29 by story 1.3. This previously read "Story 1.1 boots the app and nothing
// else ... No components, no routing, no styling", which described story 1.1's tree; story 1.2
// then shipped the token layer, and this story adds the Router, a layout, the registration page
// and the first three components.

var builder = WebAssemblyHostBuilder.CreateDefault(args);

builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// THE CULTURE PROVIDER.
//
// The WebAssembly runtime sets CurrentCulture from the browser, which is what the person asked
// for. What Yello can actually serve is a different question, and CultureSelection answers it
// from the one list both surfaces read - so the client and the Host cannot disagree about what a
// browser asking for de-AT gets. GetCultureInfo rather than a constructor: `new CultureInfo(string)`
// is a banned API at build.
//
// THE RESOLVED CULTURE IS USED FOR <html lang> AND FOR NOTHING ELSE. Do not assign it to
// CultureInfo.DefaultThreadCurrentCulture or DefaultThreadCurrentUICulture. Blazor WebAssembly
// loads one SHARD of the ICU data, chosen for the culture the runtime booted in, and RunAsync
// refuses to start when the culture has changed since boot - so that assignment stops the
// application booting at all, in every browser. The supported way to change culture is
// `blazor.start({ applicationCulture })` before the runtime boots.
//
// Nothing is lost by leaving the runtime's culture alone: ResourceManager walks en-GB -> en ->
// neutral by itself, which is CultureSelection's rule reached by a different road, and a UK
// browser gets en-GB formatting instead of being forced onto US-shaped neutral `en`.
//
// The full account - what was tried, the exact framework error, and why
// BlazorWebAssemblyLoadAllGlobalizationData is the wrong remedy here - is in deferred-work.md
// under "Nothing stops the culture-change trap being re-armed by the story that adds a second
// translation", which is the story that has to make this decision again.
var culture = CultureInfo.GetCultureInfo(
    CultureSelection.Resolve(CultureInfo.CurrentCulture, SupportedCultures.All, SupportedCultures.Default));

builder.Services.AddLocalization();

// THE HOST ADDRESS.
//
// Superseding story 1.1's "points at the client's own origin for now. The story that first calls
// the API repoints this at Yello.Host through the Aspire-injected service address." This is that
// story, and the hand-off needs one correction: Aspire injects service addresses as environment
// variables into a PROCESS, and this application runs in a browser. There is no mechanism by
// which an Aspire-injected variable reaches WebAssembly. Configuration is the equivalent that
// does work - WebAssemblyHostBuilder reads wwwroot/appsettings.json and its environment-specific
// sibling - so the address is set there, and Aspire honours Yello.Host's launch profile, which is
// what makes the development value correct under `aspire run` as well as `dotnet run`.
//
// Falling back to the client's own origin is right rather than merely safe: a same-origin
// deployment, where the Host serves the client's static files, needs no configured address at
// all. Story 1.10 owns deployment and decides which shape production takes.
//
// THE CROSS-ORIGIN CALL NOW WORKS, and story 1.4 is what made it. This comment used to predict
// that: "A CROSS-ORIGIN CALL ALSO NEEDS CORS, AND CORS IS STORY 1.4'S ... so a browser POST from
// the client's origin to the Host's is refused by the browser until that lands." It has landed.
// Yello.Host names exactly this origin in its CORS policy - injected by Yello.AppHost from this
// project's own endpoint, so the two cannot drift - and allows credentials on it.
//
// Three things are needed for a Session to survive the trip, and all three are stated once each
// rather than spread across the pages. The Host's cookie is HttpOnly, Secure and SameSite=None,
// which SessionCookiePolicy decides. The Host allows exactly this origin, with credentials and no
// wildcard, which CrossOriginPolicy decides. And every request from here asks for credentials to
// be included, which YelloApiHandler does - because `fetch` defaults to same-origin and would
// otherwise send no cookie at all, silently.
var configuredHostAddress = builder.Configuration["HostBaseAddress"];

var hostAddress = string.IsNullOrWhiteSpace(configuredHostAddress)
    ? builder.HostEnvironment.BaseAddress
    : configuredHostAddress;

// THE SESSION SEAM. Registered before the HttpClient that reads it.
//
// SessionState takes no framework type at all - no NavigationManager, no HttpClient - so the
// purge-before-announce ordering UX-DR30 requires is assertable by an ordinary test rather than
// by a browser. What "announce" means is wired here, once, and nowhere else.
builder.Services.AddScoped<SessionState>();

builder.Services.AddScoped(services =>
{
    // Constructed rather than registered through AddHttpClient: that lives in
    // Microsoft.Extensions.Http, which has no central version pin, and adding one is an
    // architecture edit rather than a convenience for a single client.
    var handler = new YelloApiHandler(services.GetRequiredService<SessionState>())
    {
        InnerHandler = new HttpClientHandler(),
    };

    return new HttpClient(handler) { BaseAddress = new Uri(hostAddress) };
});

builder.Services.AddScoped<AntiforgeryTokenSource>();

// THE SPACE STATE, from story 1.7: the switcher's last answer (Account-scoped identity, kept across
// a Space switch) and the active Space's own answer with its purge (Space-scoped, discarded on
// every departure). Both in fields, never in browser storage, and per tab.
builder.Services.AddScoped<SpaceDirectory>();
builder.Services.AddScoped<ActiveSpaceState>();

// Story 2.2: the one sender every Space-scoped write goes through (its Idempotency-Key and anti-forgery
// rules exist once), and the routed page's heading, which the skip control and a first arrival focus.
builder.Services.AddScoped<SpaceWrites>();
builder.Services.AddScoped<SpaceNarration>();
builder.Services.AddScoped<PageHeadingFocus>();

var host = builder.Build();

// WHAT HAPPENS WHEN A SESSION ENDS, wired once. AC3, UX-DR30.
//
// PURGE, THEN ANNOUNCE - and the order is SessionState's to enforce, not this line's: it runs
// every registered purge to completion before it reaches this callback. Reversing the two renders
// a queued frame one tick after the announcement, disclosing a Space the Account no longer belongs
// to, which is exactly what UX-DR30 exists to prevent.
//
// The reason travels in the address rather than in client state, because this navigation destroys
// the component tree that state would have lived in.
var sessionState = host.Services.GetRequiredService<SessionState>();
var navigation = host.Services.GetRequiredService<NavigationManager>();

sessionState.AnnounceSessionEndWith(() =>
{
    navigation.NavigateTo(ClientRoutes.SignInAfterSessionEnd);

    return Task.CompletedTask;
});

// WHERE A SIGN-OUT LANDS (story 1.7, open question 4): sign-in, with no reason - the person chose
// it, and nothing ended under them. The purge before it is the one above, not a second one.
sessionState.AnnounceSignOutWith(() =>
{
    navigation.NavigateTo(ClientRoutes.SignIn);

    return Task.CompletedTask;
});

// WHAT A SESSION'S END PURGES, beyond what components register for themselves: the active Space's
// state (and through it everything epic 2 will register there), and the switcher's list.
var activeSpace = host.Services.GetRequiredService<ActiveSpaceState>();
var spaceDirectory = host.Services.GetRequiredService<SpaceDirectory>();

sessionState.RegisterPurge(() =>
{
    activeSpace.Purge();
    spaceDirectory.Forget();

    return Task.CompletedTask;
});

// THE SYNCHRONOUS PURGE ON LEAVING A SPACE (story 1.7, AC7). A location-changing handler runs BEFORE
// the router renders the new location - for a link, a NavigateTo, and browser Back or Forward alike
// - so the departed Space is gone before anything of the destination is drawn. A navigation within
// one Space purges nothing. Registered for the life of the application; the handle is not disposed.
_ = navigation.RegisterLocationChangingHandler(context =>
{
    activeSpace.Navigating(ClientRoutes.SpaceIn(navigation.ToBaseRelativePath(context.TargetLocation)));

    return ValueTask.CompletedTask;
});

// WHETHER THIS BROWSER ALREADY HOLDS A SESSION, ASKED ONCE, BEFORE ANYTHING RENDERS.
//
// GET /api/v1/sessions/current was built for exactly this and had NO CLIENT CONSUMER AT ALL until
// code review on 2026-09-19 - the only callers in the solution were tests. The consequence was that
// SessionState.IsSignedIn, which is set only by a successful sign-in on a live component, was false
// after every reload, new tab and restored browser session, while the browser still held a valid
// eight-hour cookie. Two things followed from that, and the second is the one that mattered:
//
// A person holding a Session was shown the sign-in form again - the one thing this story's own
// sentence promises will not happen ("moving between contexts never asks me who I am again").
//
// And AC3 was unreachable. YelloApiHandler announces a 401 only to a client that BELIEVED it held a
// Session, so after a reload every 401 was swallowed silently: no purge, no navigation, nothing
// said. Every test of that path reaches its state by calling SignedIn() directly, which is a state
// the real application could not get into after a page load - so the seam suite was green against a
// path no person could take.
//
// FAILS TO "SIGNED OUT", ALWAYS. A Host that is down, slow or answering nonsense must not stop the
// client booting: this is a belief the server re-decides on every request anyway (AD-1), so the
// safe direction is obvious and every failure takes it. The short timeout is the point of the
// linked source - HttpClient's own default is 100 seconds, and a boot that hangs that long because
// the API is unreachable would be a worse bug than the one this fixes.
using (var probeTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(5)))
{
    try
    {
        var http = host.Services.GetRequiredService<HttpClient>();

        using var probe = new HttpRequestMessage(
            HttpMethod.Get,
            new Uri(SessionRoutes.Current, UriKind.Relative));
        using var answer = await http.SendAsync(probe, probeTimeout.Token);

        if (answer.StatusCode == HttpStatusCode.NoContent)
        {
            // Records the belief and drops any held anti-forgery token. There is none at boot, so
            // the second half is a no-op here - but going through the same method as a fresh
            // sign-in keeps one definition of what "this client holds a Session" does.
            sessionState.SignedIn();
        }
    }
    catch (HttpRequestException)
    {
        // The Host is unreachable. Signed out, and the surfaces report their own failures.
    }
    catch (OperationCanceledException)
    {
        // The probe timed out, or the app is shutting down. Signed out.
    }
}

// THE DOCUMENT LANGUAGE, and the reason deferred-work.md:32 named this story as its owner.
//
// base.css keys 26 locale exclusions on `:lang()`, which resolves against the document's `lang`
// attribute - and index.html hard-codes `lang="en"`, so every one of those rules was inert.
// Setting it from the culture actually being rendered is what makes them reachable. Awaited
// before RunAsync so the attribute is correct for the first paint rather than after it.
//
// The_document_language_is_set_from_the_active_culture reads Yello.Client's compiled IL for this
// call, so deleting the line fails the build rather than quietly restoring the inertness.
await DocumentLanguage.ApplyAsync(host.Services.GetRequiredService<IJSRuntime>(), culture.Name);

await host.RunAsync();
