using Yello.Application.Accounts.RegisterAccount;
using Yello.Contracts;
using Yello.Contracts.Accounts;

namespace Yello.Host.Endpoints;

/// <summary>
/// <c>POST /api/v1/accounts</c> - the registration surface's server half, and the first endpoint
/// in the product.
/// </summary>
/// <remarks>
/// <para>
/// <b>A named type, not a lambda in <c>Program.cs</c>.</b> Story 1.1 found that deleting an
/// entire startup call left 52 of 52 tests green, because a static local function inside a
/// top-level program cannot be reached by a test. <see cref="HandleAsync"/> is reachable by name,
/// and <c>HostStartupTests</c>' process-booting pattern covers the wiring around it.
/// </para>
/// <para>
/// <b>A Minimal API, not MVC</b>, and not Space-scoped: registration is what creates a Space, so
/// there is no <c>{spaceId}</c> segment for it to carry and AR-9's gate - which lists Task,
/// Project, Label and StatusDefinition - does not reach it.
/// </para>
/// <para>
/// <b>Two responses exist, and neither of them is "that address is taken".</b> A well-formed
/// submission answers <c>204 No Content</c> whether the address was new or already registered;
/// a malformed one answers <c>400</c> with an RFC 9457 body whose <c>type</c> is the contract.
/// The 400 depends only on what was sent, never on what is stored, which is what keeps it
/// compatible with AD-23 - see <c>RegisterAccountValidator</c>.
/// </para>
/// <para>
/// <b>This endpoint cannot tell the two paths apart even if it wanted to.</b>
/// <c>RegisterAccountHandler.HandleAsync</c> returns <c>Task</c>, not <c>Task&lt;bool&gt;</c>, so
/// there is no value here to branch on. That is deliberate: the cheapest route to the
/// <c>409 Conflict</c> that <c>ARCHITECTURE-SPINE.md:215</c> calls "the exact defect AD-23 exists
/// to prevent" is to hand a caller the fact and trust it not to use it.
/// </para>
/// <para>
/// <b>Idempotency (AR-25) is NOT implemented here, and no story in Epic 1 owns it.</b> Every
/// state-changing endpoint is meant to accept an <c>Idempotency-Key</c> and return the original
/// response on replay, and AR-3 forbids a slice implementing that itself - but no Epic 1 story
/// row builds the pipeline that would, and this is the product's first state-changing endpoint.
/// Recorded in <c>deferred-work.md</c> rather than improvised. The gap is unusually cheap here:
/// with no body and no identifier in the response, a replayed registration is already
/// indistinguishable from the original.
/// </para>
/// </remarks>
public static class RegisterAccountEndpoint
{
    /// <summary>
    /// Registers the endpoint on the application's route table.
    /// </summary>
    /// <remarks>
    /// The path comes from <see cref="AccountRoutes.Register"/> in <c>Yello.Contracts</c> rather
    /// than being declared here, so the client that calls it and the Host that serves it read the
    /// same string. Lee chose the route and the status on 2026-08-28; no upstream document names
    /// either.
    /// </remarks>
    /// <param name="endpoints">The route builder.</param>
    /// <returns>The registered route, for further configuration.</returns>
    public static RouteHandlerBuilder MapRegisterAccount(this IEndpointRouteBuilder endpoints) =>
        endpoints.MapPost(AccountRoutes.Register, HandleAsync);

    /// <summary>
    /// Validates a submission and hands it to the slice.
    /// </summary>
    /// <param name="request">
    /// The body. Nullable so an absent or empty one arrives here as <c>null</c> and is reported
    /// as a malformed submission, rather than throwing <c>BadHttpRequestException</c> out of the
    /// framework's binder with a response this endpoint never shaped.
    /// </param>
    /// <param name="handler">The registration slice.</param>
    /// <param name="logger">Structured output to stdout. Carries no address and no outcome.</param>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    /// <returns><c>204 No Content</c>, or <c>400</c> with a problem body.</returns>
    public static async Task<IResult> HandleAsync(
        RegisterAccountRequest? request,
        RegisterAccountHandler handler,
        ILogger<RegisterAccountEndpointMarker> logger,
        CancellationToken cancellationToken)
    {
        var command = request is null
            ? new RegisterAccountCommand(string.Empty, string.Empty, string.Empty)
            : new RegisterAccountCommand(request.DisplayName, request.EmailAddress, request.Password);

        var failures = RegisterAccountValidator.Validate(command);

        if (failures.Count > 0)
        {
            RegistrationLog.RegistrationRejected(logger, string.Join(", ", failures));

            // The title is prose and explicitly not the contract (AR-34): the client renders its
            // own localised wording keyed on `type` and on the `errors` codes, so this exists for
            // whoever is reading a log or a network trace.
            return Results.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                type: ProblemTypes.InvalidRegistration,
                title: "The registration submission was not well-formed.",
                extensions: new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["errors"] = failures,
                });
        }

        await handler.HandleAsync(command, cancellationToken).ConfigureAwait(false);

        RegistrationLog.RegistrationProcessed(logger);

        // 204 on both paths. Nothing here knows which one it took.
        return Results.NoContent();
    }
}

/// <summary>
/// The category <see cref="RegisterAccountEndpoint"/>'s logger is named for.
/// </summary>
/// <remarks>
/// <c>ILogger&lt;T&gt;</c> needs a type to take its category from, and <c>T</c> cannot be a static
/// class. This gives the endpoint's log lines a category that reads as the endpoint's own name
/// rather than borrowing an unrelated type's.
/// </remarks>
public sealed class RegisterAccountEndpointMarker
{
    /// <summary>
    /// The category name these logs are written under.
    /// </summary>
    public static string Category => typeof(RegisterAccountEndpointMarker).FullName ?? nameof(RegisterAccountEndpointMarker);
}
