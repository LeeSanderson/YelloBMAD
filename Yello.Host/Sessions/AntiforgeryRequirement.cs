using Microsoft.AspNetCore.Antiforgery;
using Yello.Contracts;

namespace Yello.Host.Sessions;

/// <summary>
/// AR-14's "every state-changing request carries an anti-forgery token", applied as one filter
/// rather than as a line inside each endpoint. AD-7.
/// </summary>
/// <remarks>
/// <para>
/// <b>An endpoint filter, applied in the composition root, is the closest this solution gets to a
/// pipeline behaviour today.</b> AR-3 forbids a slice implementing a cross-cutting invariant
/// itself, and the request pipeline stories 1.5 and 1.6 build does not exist yet. What this is
/// instead: one implementation, named once, attached to routes at the single place the routes are
/// registered - so "which endpoints are protected" is readable in <c>Program.cs</c> in four lines
/// rather than distributed across the endpoint bodies.
/// </para>
/// <para>
/// <b>Why explicit validation rather than the framework's own <c>UseAntiforgery</c>
/// metadata.</b> Both would work; this one is legible. <c>UseAntiforgery</c> validates where the
/// endpoint carries <c>IAntiforgeryMetadata</c>, which minimal-API JSON endpoints do not have by
/// default - so the protection would be present or absent according to metadata that is not
/// written anywhere in this repository, which is precisely the shape that passes a review and
/// fails in production. Calling <c>ValidateRequestAsync</c> puts the check where a reader can see
/// it fail.
/// </para>
/// <para>
/// <b>It reaches registration too, and that was a decision rather than an oversight.</b> AR-14
/// says <i>every</i> state-changing request and <c>POST /api/v1/accounts</c> is one. A token on an
/// unauthenticated endpoint protects against nothing an attacker wants - there is no Session to
/// ride - but an exception list is where this kind of rule rots, the cost is one header, and the
/// alternative is a rule with one member and a comment explaining why. Recorded as question 5 for
/// Lee, because it changes a shipped endpoint's contract.
/// </para>
/// <para>
/// <b>A <c>400</c>, and nothing is recorded in the audit store.</b> AD-20 scopes
/// <c>AccessRefusal</c> to "every 403 and every Space-boundary 404" with exactly two kinds,
/// <c>CrossSpace</c> and <c>InsufficientRole</c>; a request that never reached a Space boundary is
/// neither, and inventing a third kind to record this would put an enumeration artifact in the
/// audit store. The pipeline that writes those rows is story 1.6's and does not exist.
/// </para>
/// </remarks>
internal static class AntiforgeryRequirement
{
    /// <summary>
    /// Requires a valid anti-forgery token on the route.
    /// </summary>
    /// <param name="builder">The route being registered.</param>
    /// <returns>The same builder, for chaining.</returns>
    internal static RouteHandlerBuilder RequireAntiforgeryToken(this RouteHandlerBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        return builder.AddEndpointFilter(ValidateAsync);
    }

    private static async ValueTask<object?> ValidateAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        var antiforgery = context.HttpContext.RequestServices.GetRequiredService<IAntiforgery>();

        try
        {
            await antiforgery.ValidateRequestAsync(context.HttpContext).ConfigureAwait(false);
        }
        catch (AntiforgeryValidationException)
        {
            // The exception's own message names which half was missing or mismatched. It is
            // deliberately NOT put on the wire or into a log: it is a description of the caller's
            // request rather than of anything stored, but it is also the only place in this
            // response where prose could accumulate, and AR-34 makes the `type` the contract.
            return Results.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                type: ProblemTypes.AntiforgeryTokenMissing,
                title: "The request carried no valid anti-forgery token.");
        }

        return await next(context).ConfigureAwait(false);
    }
}
