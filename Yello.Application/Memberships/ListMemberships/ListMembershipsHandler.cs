namespace Yello.Application.Memberships.ListMemberships;

/// <summary>
/// Lists the active Space's Memberships a page at a time: display name, address and Role, in join order, with the true
/// total. FR-13, UX-DR14, UX-DR34, NFR-8; story 4.4, AC1, AC2, AC7; Q8 (a).
/// </summary>
/// <remarks>
/// <para>
/// <b>Everything cross-cutting happened before this ran</b> (AR-3): the pipeline resolved the Space and checked
/// <c>ChangeMembershipRole</c> - the list carries email addresses, which section 6.1 and AD-23 confine to Owners and Admins
/// (Lee's Q4 (a)). This slice reads no Role and no Space; both isolation layers already narrow the store to the resolved
/// Space, and the store reads each Role as its stable code.
/// </para>
/// <para>
/// <b>Paged, never unbounded</b> (NFR-8): <see cref="PageSize"/> rows a page, keyset on <c>(CreatedAt, Id)</c>, so the
/// 100-Membership bound takes two pages. The page reads one row more than it serves to learn whether another follows.
/// </para>
/// </remarks>
/// <param name="store">Reads the Memberships.</param>
public sealed class ListMembershipsHandler(IMembershipStore store)
{
    private const int Fifty = 50;

    /// <summary>
    /// The most Memberships a page holds - an assumption, stated once and tunable (the <c>ListPage</c> precedent). The server
    /// owns it: the route takes no size.
    /// </summary>
    public static int PageSize => Fifty;

    /// <summary>
    /// Reads one page.
    /// </summary>
    /// <param name="callerAccountId">The acting Account, whose own row is marked.</param>
    /// <param name="after">The cursor, or <c>null</c> for the first page.</param>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    /// <returns>The page's rows, the Space's total, and the next page's cursor text.</returns>
    public async Task<(IReadOnlyList<ListedMembership> Rows, int Total, string? NextAfter)> HandleAsync(
        Guid callerAccountId,
        MembershipCursor? after,
        CancellationToken cancellationToken)
    {
        var (rows, total) = await store.ListAsync(callerAccountId, after, PageSize + 1, cancellationToken).ConfigureAwait(false);

        if (rows.Count <= PageSize)
        {
            return (rows, total, null);
        }

        var last = rows[PageSize - 1];

        return ([.. rows.Take(PageSize)], total, MembershipCursorText.Write(new MembershipCursor(last.CreatedAt, last.Id)));
    }
}
