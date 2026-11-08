namespace Yello.Application.Spaces.OfferOwnership;

/// <summary>
/// The link an Ownership Offer email carries: the client's own address and nothing after it.
/// </summary>
/// <remarks>
/// With no usable client origin the link is empty, as an assignment link is on such a Host, which refuses every browser
/// request anyway. Offering still succeeds rather than failing on configuration it does not need.
/// </remarks>
/// <param name="ClientOrigin">The configured client origin, or <c>null</c>.</param>
public sealed record OwnershipOfferLinks(Uri? ClientOrigin)
{
    /// <summary>
    /// The client's address.
    /// </summary>
    public Uri Home =>
        ClientOrigin is { } origin
            ? new Uri(new Uri(origin.GetLeftPart(UriPartial.Authority), UriKind.Absolute), Root)
            : Root;

    private static Uri Root { get; } = new(string.Empty, UriKind.Relative);
}
