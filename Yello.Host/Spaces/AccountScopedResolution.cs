using Yello.Application.Spaces;
using Yello.Host.Sessions;

namespace Yello.Host.Spaces;

/// <summary>
/// Establishes the <see cref="AccountScopedContext"/> for an Account-scoped endpoint, as the
/// pipeline behaviour AR-3 requires it to be. AD-24 / AR-29.
/// </summary>
/// <remarks>
/// <para>
/// <b>Opted into by metadata, and the set of endpoints that opt in is closed.</b> An Account-scoped
/// endpoint carries <see cref="AccountScopedEndpoint"/> naming one of AD-24's two
/// <see cref="AccountScopedSurface"/> members, and a gate enumerates them. Unlike Space resolution,
/// which is decided by the shape of the route, this cannot be decided by shape: an Account-scoped
/// route has no parameter to recognise it by, and the risk runs the other way - a surface reading
/// across Spaces that AD-24 never sanctioned - so it has to be named.
/// </para>
/// <para>
/// <b>The Account comes from the Session the server issued</b>, never from the request (AD-2), and
/// this surface is Session-only: CAP-35 forbids any API operation enumerating Spaces beyond a
/// Token's own, so the Token surface must never reach it. It reads
/// <see cref="SessionPrincipal"/> directly rather than <see cref="ISpaceRequestEstablishment"/>,
/// which story 1.8 extended with Tokens - and the Token scheme authenticates nothing on a route
/// without <c>{spaceId}</c>, so a Token is no credential here at all.
/// </para>
/// <para>
/// <b>The answer without a Session is the authentication boundary's <c>401</c></b>, ahead of
/// anti-forgery, and nothing is recorded: AD-3 draws the refusal line at the Space boundary, and an
/// Account-scoped surface is not one. With a Session, the context is held for the request in
/// <see cref="AccountScopedRequest"/>, which the endpoint reads; the slice receives it and never
/// reads a principal.
/// </para>
/// </remarks>
internal static class AccountScopedResolution
{
    /// <summary>
    /// Marks an endpoint as one of AD-24's Account-scoped surfaces.
    /// </summary>
    /// <param name="builder">The endpoint.</param>
    /// <param name="surface">Which of the two surfaces it is.</param>
    /// <returns>The same builder, for chaining.</returns>
    internal static RouteHandlerBuilder AccountScoped(this RouteHandlerBuilder builder, AccountScopedSurface surface) =>
        builder.WithMetadata(new AccountScopedEndpoint(surface));

    /// <summary>
    /// Registers the request-scoped holder the filter fills and the endpoint reads.
    /// </summary>
    /// <param name="services">The container.</param>
    /// <returns>The same collection, for chaining.</returns>
    internal static IServiceCollection AddAccountScopedResolution(this IServiceCollection services) =>
        services.AddScoped<AccountScopedRequest>();

    /// <summary>
    /// Refuses an unauthenticated request to an Account-scoped endpoint with <c>401</c>, establishes
    /// the context for an authenticated one, and passes every other request straight through.
    /// </summary>
    /// <param name="context">The request.</param>
    /// <param name="next">The rest of the pipeline.</param>
    /// <returns>The refusal, or whatever the rest of the pipeline returns.</returns>
    internal static ValueTask<object?> EstablishAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        ArgumentNullException.ThrowIfNull(context);

        var httpContext = context.HttpContext;

        if (httpContext.GetEndpoint()?.Metadata.GetMetadata<AccountScopedEndpoint>() is null)
        {
            return next(context);
        }

        if (SessionPrincipal.AccountIdOf(httpContext.User) is not { } accountId)
        {
            return ValueTask.FromResult<object?>(BoundaryProblems.Unauthenticated());
        }

        httpContext.RequestServices.GetRequiredService<AccountScopedRequest>()
            .Establish(new AccountScopedContext(accountId));

        return next(context);
    }
}

/// <summary>
/// The metadata that makes an endpoint Account-scoped, naming which of AD-24's surfaces it is.
/// </summary>
/// <param name="Surface">The surface.</param>
internal sealed record AccountScopedEndpoint(AccountScopedSurface Surface);

/// <summary>
/// The Account-scoped context established for this request, held for the endpoint to read.
/// </summary>
/// <remarks>
/// A holder rather than the context registered as a service, because minimal APIs bind an
/// endpoint's arguments <i>before</i> its filters run - a context resolved as an argument would be
/// asked for before the filter had established it. Request-scoped, so nothing here outlives the
/// request.
/// </remarks>
public sealed class AccountScopedRequest
{
    private AccountScopedContext? _context;

    /// <summary>
    /// The context the pipeline established. Throws on an endpoint the pipeline did not establish
    /// one for, which is a wiring defect rather than a state to default.
    /// </summary>
    public AccountScopedContext Context =>
        _context ?? throw new InvalidOperationException(
            "No Account-scoped context has been established on this request. It exists only on an "
            + "endpoint marked with one of AD-24's surfaces, after the pipeline has read the Session.");

    /// <summary>
    /// Records the context. The pipeline's filter is the only caller.
    /// </summary>
    /// <param name="context">The context built from the Session.</param>
    internal void Establish(AccountScopedContext context) => _context = context;
}
