using System.Globalization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.JSInterop;
using Yello.Client;
using Yello.Client.Localisation;
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
var culture = CultureInfo.GetCultureInfo(
    CultureSelection.Resolve(CultureInfo.CurrentCulture, SupportedCultures.All, SupportedCultures.Default));

CultureInfo.DefaultThreadCurrentCulture = culture;
CultureInfo.DefaultThreadCurrentUICulture = culture;

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
// A CROSS-ORIGIN CALL ALSO NEEDS CORS, AND CORS IS STORY 1.4'S. The story's own scope table
// assigns "Sign-in, Sessions, cookies, CORS, anti-forgery" to 1.4, so a browser POST from the
// client's origin to the Host's is refused by the browser until that lands. That is a known and
// deliberate seam, not a defect in this wiring: this story's acceptance criteria are about the
// surface stating its wait and the server's behaviour, both of which are asserted directly.
var configuredHostAddress = builder.Configuration["HostBaseAddress"];

var hostAddress = string.IsNullOrWhiteSpace(configuredHostAddress)
    ? builder.HostEnvironment.BaseAddress
    : configuredHostAddress;

builder.Services.AddScoped(_ => new HttpClient { BaseAddress = new Uri(hostAddress) });

var host = builder.Build();

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
