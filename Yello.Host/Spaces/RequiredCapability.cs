using Yello.Domain.Memberships;

namespace Yello.Host.Spaces;

/// <summary>
/// Endpoint metadata: the one <see cref="Domain.Memberships.Capability"/> a Space-scoped endpoint
/// requires. FR-16 / AR-9.
/// </summary>
/// <remarks>
/// <para>
/// <b>Declared by the endpoint, checked by the pipeline.</b> An endpoint states what it does; it
/// never inspects a Role. <see cref="ActiveSpaceResolution"/> reads this, asks the Role capability
/// matrix, and answers <c>403</c> before the endpoint runs when the Role does not permit it - so a
/// later endpoint gets the check by declaring its capability, and cannot forget to perform it.
/// </para>
/// <para>
/// <b>Exactly one per Space-scoped endpoint, and no default.</b> A Space-scoped endpoint with none
/// fails closed: the pipeline refuses to serve it and the Host refuses to start with it mapped. A
/// default of "read" would be a write endpoint, one day, that every Viewer can call.
/// </para>
/// </remarks>
/// <param name="Capability">The capability required.</param>
public sealed record RequiredCapability(Capability Capability);

/// <summary>
/// Declares an endpoint's <see cref="RequiredCapability"/>.
/// </summary>
public static class RequiredCapabilityExtensions
{
    /// <summary>
    /// Declares the capability a Space-scoped endpoint requires.
    /// </summary>
    /// <param name="builder">The endpoint.</param>
    /// <param name="capability">The capability.</param>
    /// <returns>The same endpoint, for chaining.</returns>
    public static RouteHandlerBuilder RequiresCapability(this RouteHandlerBuilder builder, Capability capability) =>
        builder.WithMetadata(new RequiredCapability(capability));

    /// <summary>
    /// The capability a Space-scoped endpoint declares - exactly one, or the request fails closed.
    /// </summary>
    /// <param name="endpoint">The endpoint.</param>
    /// <returns>The declared capability.</returns>
    /// <exception cref="InvalidOperationException">The endpoint declares none, or more than one.</exception>
    internal static Capability Of(Endpoint? endpoint)
    {
        var declared = endpoint?.Metadata.GetOrderedMetadata<RequiredCapability>() ?? [];

        return declared.Count == 1
            ? declared[0].Capability
            : throw new InvalidOperationException(
                $"The Space-scoped endpoint '{endpoint?.DisplayName}' declares {declared.Count} required "
                + "capabilities. It must declare exactly one with RequiresCapability; there is no default.");
    }
}
