using System.Globalization;

namespace Yello.Host.Spaces;

/// <summary>
/// The shortest time a Space-scoped route takes to answer <c>404 not-available</c>, so a boundary refusal
/// and an in-Space not-found cannot be told apart by how long they take. AD-3; story 2.2, AC10, open
/// question 7 (a).
/// </summary>
/// <remarks>
/// <para>
/// <b>Why it exists - measured, not assumed.</b> The two answers are byte-identical, but they do different
/// work: the boundary <c>404</c> writes an <c>AccessRefusal</c> on a unit of work of its own (AD-20), and an
/// in-Space not-found - a Project id that matches nothing in the caller's own Space - writes nothing. Story
/// 2.2 measured the gap on the Board route through the one duration method (21 samples, median, 20%): 10.6%
/// to 28.8% on a Session and 11.8% to 15.2% on a Token across three runs, and one run in three failed.
/// A timing row caught one time in three is not a gate, and an in-Space id that answers faster than a
/// foreign one is exactly the oracle AD-3 forbids.
/// </para>
/// <para>
/// <b>One place, around the whole of Space resolution.</b> This filter runs on the API group immediately
/// before <see cref="ActiveSpaceResolution.ResolveAsync"/>, times everything after it, and when the answer
/// is a <c>404</c> holds it until the floor has elapsed. Every not-found a Space-scoped route gives is the
/// one <c>not-available</c> body, so the rule needs no list of causes. It costs latency on not-found answers
/// only, never on a success, and changes no AD.
/// </para>
/// <para>
/// <b>The unmatched fallback is outside it</b>, deliberately: it is mapped on the application, not the group,
/// so the timing suite's permanent oracle (a boundary <c>404</c> against an unmatched path) still separates.
/// A floor that swallowed the oracle would make the timing rows unfalsifiable.
/// </para>
/// <para>
/// <b>Configurable, and logged at startup</b> (<see cref="ConfigurationKey"/>). It must sit above both
/// paths' 95th percentile where the Host runs; <c>0</c> turns it off, which the startup line says.
/// </para>
/// </remarks>
internal static class NotAvailableFloor
{
    /// <summary>
    /// Where the floor is configured, in whole milliseconds.
    /// </summary>
    internal const string ConfigurationKey = "SpaceBoundary:NotAvailableFloorMilliseconds";

    /// <summary>
    /// The default: several times both paths' measured medians on a developer machine (8-12 ms), and above
    /// their tails against a remote database.
    /// </summary>
    internal static TimeSpan Default { get; } = TimeSpan.FromMilliseconds(50);

    /// <summary>
    /// The longest floor the setting accepts: a not-found held longer than this is a misconfiguration that
    /// would pile requests up, not a timing defence.
    /// </summary>
    internal static TimeSpan Longest { get; } = TimeSpan.FromSeconds(2);

    /// <summary>
    /// Reads the configured floor.
    /// </summary>
    /// <param name="configured">The configured value, which may be absent.</param>
    /// <returns>The floor, and the problem with the configured value when it was not used.</returns>
    internal static FloorSetting ReadSetting(string? configured)
    {
        if (string.IsNullOrWhiteSpace(configured))
        {
            return new FloorSetting(Default, Problem: null);
        }

        if (!int.TryParse(configured, NumberStyles.None, CultureInfo.InvariantCulture, out var milliseconds))
        {
            return new FloorSetting(Default, $"'{configured}' is not a whole number of milliseconds. Falling back to the default.");
        }

        var floor = TimeSpan.FromMilliseconds(milliseconds);

        return floor <= Longest
            ? new FloorSetting(floor, Problem: null)
            : new FloorSetting(Default, $"{milliseconds} ms is above the longest floor, {Longest.TotalMilliseconds} ms. Falling back to the default.");
    }

    /// <summary>
    /// Holds a Space-scoped request's <c>404</c> until the floor has elapsed, and passes every other answer,
    /// and every request that is not Space-scoped, straight through.
    /// </summary>
    /// <param name="context">The request.</param>
    /// <param name="next">The rest of the pipeline.</param>
    /// <returns>Whatever the rest of the pipeline returns.</returns>
    internal static ValueTask<object?> HoldAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        ArgumentNullException.ThrowIfNull(context);

        return ActiveSpaceResolution.IsSpaceScoped(context.HttpContext)
            ? HoldCoreAsync(context, next)
            : next(context);
    }

    private static async ValueTask<object?> HoldCoreAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var services = context.HttpContext.RequestServices;
        var clock = services.GetService<TimeProvider>() ?? TimeProvider.System;

        // Absent only from a probe application that registers no Host services; it fails closed to the
        // default rather than to no floor.
        var floor = services.GetService<FloorSetting>()?.Floor ?? Default;

        var started = clock.GetTimestamp();
        var result = await next(context).ConfigureAwait(false);

        if (result is IStatusCodeHttpResult { StatusCode: StatusCodes.Status404NotFound })
        {
            var remaining = floor - clock.GetElapsedTime(started);

            if (remaining > TimeSpan.Zero)
            {
                // Not the request's token: a client that disconnects does not get to learn the duration by
                // cancelling, and the wait holds no lock and no database connection
                // (the unit of work has ended), though the client's request stays open for it.
                await Task.Delay(remaining, clock, CancellationToken.None).ConfigureAwait(false);
            }
        }

        return result;
    }
}

/// <summary>
/// The resolved not-found floor.
/// </summary>
/// <param name="Floor">The floor; zero turns it off.</param>
/// <param name="Problem">Why the configured value was not used, or <c>null</c>.</param>
internal sealed record FloorSetting(TimeSpan Floor, string? Problem);
