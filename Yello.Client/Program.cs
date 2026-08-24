using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Yello.Client;

// The client's composition root. Story 1.1 boots the app and nothing else: AC4 requires
// Yello.Client to be running under the AppHost, which is the whole of its job here.
// No components, no routing, no styling - see App.razor and wwwroot/index.html.

var builder = WebAssemblyHostBuilder.CreateDefault(args);

builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// Points at the client's own origin for now. The story that first calls the API repoints
// this at Yello.Host through the Aspire-injected service address.
builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

await builder.Build().RunAsync();
