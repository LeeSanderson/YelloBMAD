using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Yello.Application.Spaces;
using Yello.Domain.Memberships;
using Yello.Host;
using Yello.Host.Spaces;
using Yello.Infrastructure;

namespace Yello.Tests.Slices.Spaces.ResolveActiveSpace;

/// <summary>
/// The Space boundary's wiring, without a database: the order of the route group's filters, the
/// startup check that no <c>{spaceId}</c> route is mapped around them, and the purge's cadence.
/// Story 1.6 Tasks 5, 7 and 8.
/// </summary>
/// <remarks>
/// <b>No class fixture, deliberately.</b> Nothing here needs a database, so nothing here starts an
/// engine - and so these run when the container runtime is down.
/// </remarks>
[Trait("Suite", "Slices")]
[Trait("Priority", "P1")]
[Trait("Requirement", "AR-3")]
[Trait("Requirement", "AR-9")]
[Trait("Requirement", "AD-3")]
public sealed class SpaceBoundaryRouteTests
{
    private const string SpaceScopedProbe = "/api/v1/spaces/{spaceId:guid}/probe";
    private const string UnscopedProbe = "/api/v1/probe";

    /// <summary>
    /// Story 1.6 decision 11 - a state-changing request to a Space-scoped route with no Session and no
    /// anti-forgery token is answered <c>401</c>, the authentication boundary, not the anti-forgery
    /// <c>400</c>.
    /// </summary>
    /// <remarks>
    /// Through the real group, by invoking the endpoint's built request delegate - which carries the
    /// group's filters in the order <see cref="ApiRouteGroup"/> attaches them. Anchored on the same
    /// tokenless request to a route that is NOT Space-scoped being answered <c>400</c>, so the
    /// anti-forgery filter demonstrably runs in this harness and the <c>401</c> is the order, not its
    /// absence.
    /// </remarks>
    [Fact]
    public async Task An_unauthenticated_state_change_to_a_Space_scoped_route_is_answered_401_before_anti_forgery()
    {
        await using var app = ProbeApplication(app =>
        {
            var api = app.MapApiGroup();

            api.MapPost(SpaceScopedProbe, () => Results.NoContent()).RequiresCapability(Capability.ManageTasks);
            api.MapPost(UnscopedProbe, () => Results.NoContent());
        });

        Assert.Equal(StatusCodes.Status400BadRequest, await PostAsync(app, UnscopedProbe, routeValues: null));
        Assert.Equal(StatusCodes.Status401Unauthorized, await PostAsync(app, SpaceScopedProbe, Guid.NewGuid()));
    }

    /// <summary>
    /// AR-9 / the 1.5 review's ledger entry - a <c>{spaceId}</c> route mapped anywhere but the
    /// resolving group, or declaring no capability, is reported; one mapped through the group with
    /// its capability is not.
    /// </summary>
    /// <remarks>
    /// The Host calls the same check at startup and refuses to start on any report, so the real route
    /// table is held by every test that boots a Host; this case holds the check itself.
    /// </remarks>
    [Fact]
    public async Task A_spaceId_route_outside_the_resolving_group_or_without_a_capability_is_reported()
    {
        await using var app = ProbeApplication(app =>
        {
            var api = app.MapApiGroup();

            api.MapGet("/api/v1/spaces/{spaceId:guid}/declared", () => Results.NoContent())
                .RequiresCapability(Capability.ReadProjectsAndTasks);
            api.MapGet("/api/v1/spaces/{spaceId:guid}/undeclared", () => Results.NoContent());
            app.MapGet("/api/v1/spaces/{spaceId:guid}/around", () => Results.NoContent())
                .RequiresCapability(Capability.ReadProjectsAndTasks);
            app.MapGet(UnscopedProbe, () => Results.NoContent());
        });

        IEndpointRouteBuilder routes = app;
        var problems = SpaceScopedRouteCheck.ProblemsIn(routes.DataSources.SelectMany(source => source.Endpoints));

        Assert.Equal(2, problems.Count);
        Assert.Contains(problems, problem => problem.Contains("/undeclared", StringComparison.Ordinal)
            && problem.Contains("0 required capabilities", StringComparison.Ordinal));
        Assert.Contains(problems, problem => problem.Contains("/around", StringComparison.Ordinal)
            && problem.Contains("not mapped through", StringComparison.Ordinal));
        Assert.DoesNotContain(problems, problem => problem.Contains("/declared", StringComparison.Ordinal));

        Assert.Throws<InvalidOperationException>(() => SpaceScopedRouteCheck.EnsureEveryRouteIsResolved(app));
    }

    /// <summary>
    /// AC4 / AD-10 - the refusal purge runs at most once a day per process, decided on the injected
    /// clock: a call inside the day returns without touching the database, and the first call a day
    /// later tries again.
    /// </summary>
    /// <remarks>
    /// With no connection string configured, an attempted purge fails at once inside EF Core - which
    /// is exactly the observable needed: a faulted task means the purge reached for the database, a
    /// completed one means it did not. No <c>Task.Delay</c>; the clock is a hand-written
    /// <see cref="TimeProvider"/>.
    /// </remarks>
    [Fact]
    [Trait("Requirement", "AR-33")]
    [Trait("Requirement", "AD-10")]
    [Trait("Requirement", "AD-20")]
    public async Task The_refusal_purge_reaches_for_the_database_at_most_once_a_day()
    {
        var clock = new SettableClock(new DateTimeOffset(2026, 10, 24, 9, 0, 0, TimeSpan.Zero));

        await using var provider = new ServiceCollection()
            .AddLogging()
            .AddSingleton<TimeProvider>(clock)
            .AddYelloInfrastructure(connectionString: null)
            .BuildServiceProvider();

        var retention = provider.GetRequiredService<IRefusalRetention>();

        Assert.True(await ReachedForTheDatabaseAsync(retention), "The first request of a process did not purge.");

        clock.Now += TimeSpan.FromHours(23) + TimeSpan.FromMinutes(59);
        Assert.False(await ReachedForTheDatabaseAsync(retention), "A second purge ran inside the same day.");

        clock.Now += TimeSpan.FromMinutes(1);
        Assert.True(await ReachedForTheDatabaseAsync(retention), "No purge ran once a day had passed.");

        Assert.False(await ReachedForTheDatabaseAsync(retention), "The purge ran twice on the same instant.");
    }

    private static async Task<bool> ReachedForTheDatabaseAsync(IRefusalRetention retention)
    {
        try
        {
            await retention.PurgeIfDueAsync(TestContext.Current.CancellationToken);

            return false;
        }
        catch (InvalidOperationException)
        {
            return true;
        }
    }

    private static WebApplication ProbeApplication(Action<WebApplication> map)
    {
        var builder = WebApplication.CreateSlimBuilder();

        builder.Services.AddAntiforgery();
        builder.Services.AddSingleton<ISpaceRequestEstablishment, SessionSpaceRequestEstablishment>();

        var app = builder.Build();
        map(app);

        return app;
    }

    private static async Task<int> PostAsync(WebApplication app, string pattern, Guid? routeValues)
    {
        IEndpointRouteBuilder routes = app;

        var endpoint = routes.DataSources
            .SelectMany(source => source.Endpoints)
            .OfType<RouteEndpoint>()
            .Single(candidate => candidate.RoutePattern.RawText == pattern);

        await using var scope = app.Services.CreateAsyncScope();

        var httpContext = new DefaultHttpContext { RequestServices = scope.ServiceProvider };
        httpContext.Request.Method = HttpMethods.Post;
        httpContext.SetEndpoint(endpoint);

        if (routeValues is { } spaceId)
        {
            httpContext.Request.RouteValues["spaceId"] = spaceId.ToString();
        }

        var handler = endpoint.RequestDelegate;
        Assert.NotNull(handler);

        await handler(httpContext);

        return httpContext.Response.StatusCode;
    }

    /// <summary>
    /// A clock a test moves by hand.
    /// </summary>
    private sealed class SettableClock(DateTimeOffset start) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = start;

        public override DateTimeOffset GetUtcNow() => Now;
    }
}
