namespace Yello.Contracts.Memberships;

/// <summary>
/// One page of the Space's Membership list. Story 4.4, AC1, Q8 (a); NFR-8, AD-29.
/// </summary>
/// <remarks>
/// 50 Memberships a page in join order, so the 100-Membership bound takes two pages; the client appends a page and never
/// recycles a row.
/// </remarks>
/// <param name="Total">The Space's true Membership count - a separate count, never the page length.</param>
/// <param name="Memberships">This page's rows, in join order.</param>
/// <param name="NextAfter">The opaque cursor for the next page, or <c>null</c> on the last.</param>
public sealed record MembershipPageResponse(int Total, IReadOnlyList<MembershipResponse> Memberships, string? NextAfter);
