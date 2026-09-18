using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Yello.Application.Accounts.AuthenticateAccount;
using Yello.Contracts;
using Yello.Contracts.Accounts;
using Yello.Host.Sessions;

namespace Yello.Host.Endpoints;

/// <summary>
/// The Session resource: <c>POST /api/v1/sessions</c> to establish one,
/// <c>GET /api/v1/sessions/current</c> to ask whether the caller still holds one, and
/// <c>GET /api/v1/antiforgery/token</c> for the token the first of those must carry.
/// </summary>
/// <remarks>
/// <para>
/// <b>Named types with named methods, not lambdas in <c>Program.cs</c>.</b> Story 1.1 found that
/// deleting an entire startup call left 52 of 52 tests green, because a static local function
/// inside a top-level program cannot be reached by a test.
/// </para>
/// <para>
/// <b>AD-23's uniformity lives here, in the endpoint, rather than in the password comparator.</b>
/// <c>harness-constraints.md:66</c> is explicit that the guarantee "must hold identically on the
/// OAuth path, which is a new disclosure surface" - so it is implemented over the slice's single
/// <c>null</c>, where every future credential path will also arrive, rather than inside the thing
/// that happens to compare passwords today. There is exactly one failure response in
/// <see cref="AuthenticateAsync"/> and no value anywhere in this file that says which cause
/// produced it.
/// </para>
/// <para>
/// <b>These endpoints read <c>HttpContext.User</c> rather than carrying <c>[Authorize]</c>.</b>
/// No <c>AddAuthorization</c>, no <c>UseAuthorization</c> and no policy: the authorisation
/// pipeline is stories 1.5 and 1.6's, and AD-1 makes every authorisation decision a per-request
/// <c>(Account, Space)</c> resolution that nothing here can perform. What
/// <see cref="CurrentSession"/> asks is strictly the <i>authentication</i> question - did the cookie
/// handler produce an authenticated principal - which is the boundary Task 8 requires an expired
/// Session to be refused at, and it is refused with a 401 rather than the framework's default 302.
/// </para>
/// </remarks>
public static class SessionEndpoints
{
    /// <summary>
    /// Registers <c>POST /api/v1/sessions</c>.
    /// </summary>
    /// <param name="endpoints">The route builder.</param>
    /// <returns>The registered route, for further configuration.</returns>
    public static RouteHandlerBuilder MapAuthenticate(this IEndpointRouteBuilder endpoints) =>
        endpoints.MapPost(SessionRoutes.Authenticate, AuthenticateAsync);

    /// <summary>
    /// Registers <c>GET /api/v1/sessions/current</c>.
    /// </summary>
    /// <param name="endpoints">The route builder.</param>
    /// <returns>The registered route, for further configuration.</returns>
    public static RouteHandlerBuilder MapCurrentSession(this IEndpointRouteBuilder endpoints) =>
        endpoints.MapGet(SessionRoutes.Current, CurrentSession);

    /// <summary>
    /// Registers <c>GET /api/v1/antiforgery/token</c>.
    /// </summary>
    /// <param name="endpoints">The route builder.</param>
    /// <returns>The registered route, for further configuration.</returns>
    public static RouteHandlerBuilder MapAntiforgeryToken(this IEndpointRouteBuilder endpoints) =>
        endpoints.MapGet(SessionRoutes.AntiforgeryToken, IssueAntiforgeryToken);

    /// <summary>
    /// Verifies a credential and, on success, issues the Session cookie.
    /// </summary>
    /// <param name="request">
    /// The body. Nullable so an absent or empty one arrives here as <c>null</c> and is reported as
    /// a malformed submission, rather than throwing <c>BadHttpRequestException</c> out of the
    /// framework's binder with a response this endpoint never shaped - the same reasoning, and the
    /// same shape, as <c>RegisterAccountEndpoint</c>.
    /// </param>
    /// <param name="handler">The authentication slice.</param>
    /// <param name="httpContext">The request, which the cookie is written onto.</param>
    /// <param name="logger">Structured output. Carries no address, and no outcome.</param>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    /// <returns><c>204</c> with a <c>Set-Cookie</c>, <c>401</c>, or <c>400</c>.</returns>
    public static Task<IResult> AuthenticateAsync(
        SignInRequest? request,
        AuthenticateAccountHandler handler,
        HttpContext httpContext,
        ILogger<SessionEndpointMarker> logger,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        // Split from the body below because S4457 requires an argument check to run eagerly rather
        // than on the first await of a state machine. It is the framework that supplies this
        // argument, so the guard is a rule's price rather than a live risk.
        return HandleAuthenticateAsync(request, handler, httpContext, logger, cancellationToken);
    }

    private static async Task<IResult> HandleAuthenticateAsync(
        SignInRequest? request,
        AuthenticateAccountHandler handler,
        HttpContext httpContext,
        ILogger<SessionEndpointMarker> logger,
        CancellationToken cancellationToken)
    {
        var command = request is null
            ? new AuthenticateAccountCommand(string.Empty, string.Empty)
            : new AuthenticateAccountCommand(request.EmailAddress, request.Password);

        var failures = AuthenticateAccountValidator.Validate(command);

        if (failures.Count > 0)
        {
            SessionLog.SignInRejected(logger, string.Join(", ", failures));

            // A different response from the 401 below, and that is compatible with AD-23 for
            // exactly the reason story 1.3 recorded for registration: every rule in the validator
            // reads only the submitted values, so no outcome of it can vary with what is stored.
            // A submission that reaches this branch was never an authentication attempt.
            return Results.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                type: ProblemTypes.InvalidSignIn,
                title: "The sign-in submission was not well-formed.",
                extensions: new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["errors"] = failures,
                });
        }

        var authenticated = await handler.HandleAsync(command, cancellationToken).ConfigureAwait(false);

        if (authenticated is null)
        {
            SessionLog.SignInProcessed(logger);

            // ONE RESPONSE FOR EVERY CAUSE. An unknown address and a known address with a wrong
            // password both arrive here as the same null, so there is no value in scope to shape a
            // second response from even if a later story wanted to. No `errors` extension, because
            // an extension is a place a cause would eventually be written.
            return Results.Problem(
                statusCode: StatusCodes.Status401Unauthorized,
                type: ProblemTypes.AuthenticationFailed,
                title: "Authentication did not succeed.");
        }

        await httpContext.SignInAsync(
                SessionCookiePolicy.SchemeName,
                SessionPrincipal.For(authenticated.AccountId),
                new AuthenticationProperties { IsPersistent = true })
            .ConfigureAwait(false);

        // The same line the failure path writes, deliberately. See SessionLog: nothing in this
        // endpoint's output distinguishes a success from either failure cause, because a uniform
        // response undone by a log line is the same leak by a slower route.
        SessionLog.SignInProcessed(logger);

        return Results.NoContent();
    }

    /// <summary>
    /// Answers whether the caller holds a Session that is still good.
    /// </summary>
    /// <param name="httpContext">The request, whose principal the cookie handler has populated.</param>
    /// <returns><c>204</c> when the Session authenticates, <c>401</c> when it does not.</returns>
    /// <remarks>
    /// <para>
    /// <b>This is where AC3 is actually observable.</b> The cookie handler evaluates the ticket's
    /// expiry when it reads the cookie, so an expired or tampered ticket produces an
    /// unauthenticated principal and this answers 401 - without a database read, without a sweeper,
    /// and without anything on a timer (AD-10, AD-27).
    /// </para>
    /// <para>
    /// <b>It grants nothing, including to Spaces the Account still holds a Membership in</b>, for
    /// the structural reason that it grants nothing to anybody: there is no Space-scoped surface
    /// in the product until story 1.5. That is a limit of what this story can demonstrate, not a
    /// claim that the clause is proved.
    /// </para>
    /// </remarks>
    public static IResult CurrentSession(HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        // The claim is read rather than IsAuthenticated alone: a principal authenticated by some
        // future second scheme but carrying no Account identity is not a Yello Session, and
        // answering 204 for it would be the first crack in AC1.
        return SessionPrincipal.AccountIdOf(httpContext.User) is null
            ? Results.Problem(
                statusCode: StatusCodes.Status401Unauthorized,
                type: ProblemTypes.AuthenticationFailed,
                title: "Authentication did not succeed.")
            : Results.NoContent();
    }

    /// <summary>
    /// Issues the anti-forgery request token, and sets the cookie half beside it.
    /// </summary>
    /// <param name="antiforgery">The token system, configured once in the composition root.</param>
    /// <param name="httpContext">The request the cookie is written onto.</param>
    /// <returns>The request token.</returns>
    public static IResult IssueAntiforgeryToken(IAntiforgery antiforgery, HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(antiforgery);

        var tokens = antiforgery.GetAndStoreTokens(httpContext);

        // RequestToken is documented as non-null for a context this method has just stored tokens
        // on; the null-forgiving operator says that rather than a defensive branch that could never
        // be covered by a test.
        return Results.Ok(new AntiforgeryTokenResponse(tokens.RequestToken!));
    }
}

/// <summary>
/// The category <see cref="SessionEndpoints"/>' logger is named for.
/// </summary>
/// <remarks>
/// <c>ILogger&lt;T&gt;</c> needs a type to take its category from, and <c>T</c> cannot be a static
/// class. S2094 refuses an empty one, which is why this carries the member below.
/// </remarks>
public sealed class SessionEndpointMarker
{
    /// <summary>
    /// The category name these logs are written under.
    /// </summary>
    public static string Category => typeof(SessionEndpointMarker).FullName ?? nameof(SessionEndpointMarker);
}
