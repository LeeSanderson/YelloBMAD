using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Yello.Contracts.Spaces;
using Yello.Host.Spaces;

namespace Yello.Tests.Slices.Spaces.ResolveActiveSpace;

/// <summary>
/// The not-found floor holds a <c>404</c> until its own clock says the floor has passed, however the timer behaves.
/// </summary>
/// <remarks>
/// A single <c>Task.Delay</c> is not enough: on Windows a timer fires on the ~15.6 ms system tick, up to a tick early,
/// and <c>Task.Delay</c> truncates to whole milliseconds. The answer then ended a tick before or after the floor
/// depending on how long the work took, and the isolation timing rows read two answers whose work matched as a whole
/// tick apart. The clock here is hand-driven and its timers fire early on purpose, so the case is deterministic.
/// </remarks>
[Trait("Suite", "Slices")]
[Trait("Priority", "P0")]
[Trait("Requirement", "AD-3")]
public sealed class NotAvailableFloorTests
{
    private static readonly TimeSpan Floor = TimeSpan.FromMilliseconds(50);

    /// <summary>
    /// The <c>404</c> is released at the floor, never before it: not when the timer fires early, and not when the
    /// remaining wait has a fraction of a millisecond that <c>Task.Delay</c> would drop.
    /// </summary>
    /// <param name="workMicroseconds">How long the endpoint took before it answered.</param>
    /// <param name="earlyMilliseconds">How early each timer fires.</param>
    /// <returns>A task.</returns>
    [Theory]
    [InlineData(5_000, 10)]
    [InlineData(4_600, 0)]
    [InlineData(12_300, 15)]
    public async Task A_not_found_is_held_until_the_clock_reaches_the_floor(int workMicroseconds, int earlyMilliseconds)
    {
        var clock = new EarlyTimerClock(TimeSpan.FromMilliseconds(earlyMilliseconds));
        var notFound = Results.NotFound();
        var context = SpaceScopedRequest(clock);
        var started = clock.GetTimestamp();

        var answered = await NotAvailableFloor.HoldAsync(context, _ =>
        {
            clock.Advance(TimeSpan.FromMicroseconds(workMicroseconds));
            return ValueTask.FromResult<object?>(notFound);
        });

        var elapsed = clock.GetElapsedTime(started);

        Assert.Same(notFound, answered);
        Assert.True(elapsed >= Floor, $"Released after {elapsed.TotalMilliseconds} ms, before the {Floor.TotalMilliseconds} ms floor.");
        Assert.True(elapsed < Floor + TimeSpan.FromMilliseconds(1), $"Released after {elapsed.TotalMilliseconds} ms, well past the floor.");
    }

    private static DefaultEndpointFilterInvocationContext SpaceScopedRequest(TimeProvider clock)
    {
        var httpContext = new DefaultHttpContext
        {
            RequestServices = new ServiceCollection()
                .AddSingleton(clock)
                .AddSingleton(new FloorSetting(Floor, Problem: null))
                .BuildServiceProvider(),
        };

        httpContext.SetEndpoint(new RouteEndpoint(
            _ => Task.CompletedTask,
            RoutePatternFactory.Parse($"/spaces/{{{SpaceRoutes.SpaceIdParameter}}}"),
            order: 0,
            EndpointMetadataCollection.Empty,
            displayName: "space-scoped"));

        return new DefaultEndpointFilterInvocationContext(httpContext);
    }

    /// <summary>
    /// A clock that moves only when the work or a timer moves it, and whose timers fire <c>early</c> before they are due.
    /// </summary>
    private sealed class EarlyTimerClock(TimeSpan early) : TimeProvider
    {
        private long _ticks;

        public override long TimestampFrequency => TimeSpan.TicksPerSecond;

        public override long GetTimestamp() => Interlocked.Read(ref _ticks);

        public void Advance(TimeSpan by) => Interlocked.Add(ref _ticks, by.Ticks);

        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            Advance(dueTime > early ? dueTime - early : dueTime);
            ThreadPool.UnsafeQueueUserWorkItem(_ => callback(state), state: null);

            return new FiredTimer();
        }

        private sealed class FiredTimer : ITimer
        {
            public bool Change(TimeSpan dueTime, TimeSpan period) => false;

            public void Dispose()
            {
            }

            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }
    }
}
