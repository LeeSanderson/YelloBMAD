using Microsoft.AspNetCore.Antiforgery;
using Yello.Contracts;
using Yello.Host.ApiTokens;

namespace Yello.Host.Sessions;

/// <summary>
/// AR-14's "every state-changing request carries an anti-forgery token", applied as one filter
/// rather than as a line inside each endpoint. AD-7.
/// </summary>
/// <remarks>
/// <para>
/// <b>An endpoint filter, applied in the composition root, is how this solution expresses a
/// pipeline behaviour.</b> AR-3 forbids a slice implementing a cross-cutting invariant itself, so
/// this is one implementation, named once, attached at the single place the routes are registered -
/// the same shape <c>ActiveSpaceResolution</c> takes beside it.
/// </para>
/// <para>
/// <b>Attached by HTTP method rather than by a list of routes, and that changed at code review on
/// 2026-09-19.</b> It was a per-route opt-in - <c>app.MapAuthenticate().RequireAntiforgeryToken()</c>
/// - which made "which endpoints are protected" readable in four lines and made the next story to
/// add a <c>POST</c> and forget the call ship an unprotected state change with every test in this
/// story green. The comment below already said an exception list is where this kind of rule rots;
/// the list of protected routes rots the same way, and more quietly, because forgetting to add to it
/// looks like nothing at all. <see cref="ValidateStateChangingAsync"/> is now attached to the whole
/// route group and validates every request whose method is not in RFC 9110's safe set, so a future
/// verb is covered because of what it is. That is the same test
/// <c>YelloApiHandler.RequiresAntiforgeryToken</c> applies on the client side - the two halves of
/// AR-14 are finally phrased alike.
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
/// <b>It reaches registration too, and question 5 is now settled by construction.</b> AR-14 says
/// <i>every</i> state-changing request and <c>POST /api/v1/accounts</c> is one. A token on an
/// unauthenticated endpoint protects against nothing an attacker wants - there is no Session to
/// ride - but an exception list is where this kind of rule rots, the cost is one header, and the
/// alternative is a rule with one member and a comment explaining why. This was recorded as
/// question 5 for Lee because it changes a shipped endpoint's contract; Lee answered it at code
/// review on 2026-09-19 by taking the method-shaped rule, under which registration carries a token
/// because it is a <c>POST</c> rather than because anybody decided it should. There is no exception
/// list to keep.
/// </para>
/// <para>
/// <b>A <c>400</c>, and nothing is recorded in the audit store.</b> AD-20 scopes
/// <c>AccessRefusal</c> to "every 403 and every Space-boundary 404" with exactly two kinds,
/// <c>CrossSpace</c> and <c>InsufficientRole</c>; a request that never reached a Space boundary is
/// neither, and inventing a third kind to record this would put an enumeration artifact in the
/// audit store. <c>ActiveSpaceResolution</c> writes those rows, and never for this.
/// </para>
/// </remarks>
internal static class AntiforgeryRequirement
{
    /// <summary>
    /// RFC 9110's safe methods - the ones that do not change state and so carry no token.
    /// </summary>
    /// <remarks>
    /// The same set, spelled the same way, as <c>YelloApiHandler.RequiresAntiforgeryToken</c> on the
    /// client. Stated as what is <i>exempt</i> rather than as what is covered, so a verb a later
    /// story reaches for is protected by default rather than by remembering to add it.
    /// </remarks>
    private static readonly HashSet<string> SafeMethods = new(StringComparer.OrdinalIgnoreCase)
    {
        HttpMethods.Get,
        HttpMethods.Head,
        HttpMethods.Options,
        HttpMethods.Trace,
    };

    /// <summary>
    /// Validates the anti-forgery token on every request whose method changes state. Registered
    /// once on the route group in the composition root, so it covers every endpoint in it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The method is read per request rather than from endpoint metadata.</b>
    /// <c>EndpointFilterFactoryContext</c> exposes the handler's <c>MethodInfo</c> and the
    /// application's services - not the route's <c>HttpMethodMetadata</c> - so a factory cannot
    /// decide this at build time. Reading <c>Request.Method</c> is better anyway: a route mapped for
    /// several verbs gets the right answer for each one, which a single build-time decision could
    /// not give.
    /// </para>
    /// <para>
    /// <b>It fails closed.</b> Anything not in <see cref="SafeMethods"/> is validated, so a verb
    /// nobody anticipated is protected rather than exempt.
    /// </para>
    /// </remarks>
    /// <param name="context">The request.</param>
    /// <param name="next">The rest of the pipeline.</param>
    /// <returns>The refusal, or whatever the endpoint returns.</returns>
    internal static ValueTask<object?> ValidateStateChangingAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        ArgumentNullException.ThrowIfNull(context);

        // Not async, so that S4457's argument check runs eagerly rather than on the first await of
        // a state machine - the same split SessionEndpoints makes, for the same rule. A safe method
        // is handed straight to the endpoint without entering the validating path at all.
        //
        // So is a request an API Token authenticated (story 1.8, AC12): a bearer header is not an
        // ambient credential, so a third-party page has nothing to ride, and a script has no
        // anti-forgery cookie to pair a token with. The exemption is exactly the Token's - a
        // principal only ApiTokenAuthenticationHandler builds, and only on a Space-scoped route that
        // admits Tokens. A request carrying the Session cookie and no bearer is validated as before.
        // One carrying a valid bearer as well is authenticated by the bearer alone (open question 5),
        // so it is exempt too: its cookie grants it nothing, and a third-party page cannot attach a
        // bearer, since CORS admits one origin. One carrying an invalid bearer has no principal, so it
        // is answered exactly as a request with no credential is.
        return SafeMethods.Contains(context.HttpContext.Request.Method)
            || ApiTokenPrincipal.IsToken(context.HttpContext.User)
            ? next(context)
            : ValidateAsync(context, next);
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
            return TokenRefused();
        }
        catch (InvalidDataException)
        {
            // NOT an AntiforgeryValidationException, and it escaped to a 500 until code review on
            // 2026-09-19. ValidateRequestAsync reads the form whenever the request declares a form
            // content type, so a POST with `Content-Type: multipart/form-data; boundary=x` and a
            // truncated body throws out of the multipart reader instead. Unauthenticated and
            // trivially reproducible on both state-changing endpoints.
            //
            // Answered as a refused token rather than as a distinct problem type: from outside,
            // a request whose anti-forgery token could not be read is a request that carried no
            // valid token, and inventing a second `type` would tell an unauthenticated caller
            // something about how the Host parses their body. AR-34 makes the `type` the contract.
            return TokenRefused();
        }
        catch (BadHttpRequestException)
        {
            // The same defect one layer down: the form reader raises this for a body that exceeds
            // its limits or ends early. Same answer, for the same reason.
            return TokenRefused();
        }

        return await next(context).ConfigureAwait(false);
    }

    private static IResult TokenRefused() =>
        Results.Problem(
            statusCode: StatusCodes.Status400BadRequest,
            type: ProblemTypes.AntiforgeryTokenMissing,
            title: "The request carried no valid anti-forgery token.");
}
