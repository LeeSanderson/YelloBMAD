namespace Yello.Tests.Revocation;

/// <summary>
/// The entry point of the scaffold contract. Throws until story 1.9 Task 3 gives it a harness.
/// </summary>
internal static class RevocationWorld
{
    public static readonly RevocationSurface[] Surfaces = Enum.GetValues<RevocationSurface>();

    /// <summary>
    /// A fresh Account, registered through the product, signed in, holding one Token for its Personal
    /// Space - on the class's one running Host. Each call seeds a new Account, because every case here
    /// destroys what it was given.
    /// </summary>
    public static Task<IRevocationWorld> StartAsync() =>
        throw new InvalidOperationException(
            "The revocation harness does not exist yet (story 1.9 Task 2: one engine per test process; Task 3: RunningHost, "
            + "surface actor, product seeding; Task 7: the raw-SQL events).");
}

/// <summary>
/// The two surfaces every revocation case runs on (AD-4).
/// </summary>
/// <remarks>
/// <b>Red-phase scaffold contract.</b> Replaced by the story's <c>IsolationSurface</c> from
/// <c>Yello.Tests.Shared</c> once Task 3 has moved the harness there.
/// </remarks>
internal enum RevocationSurface
{
    Session,
    ApiToken,
}

/// <summary>
/// One wire answer: status, content type, body and header names (<c>Date</c> excluded).
/// </summary>
internal interface IRevocationAnswer
{
    int Status { get; }

    string? ContentType { get; }

    string Body { get; }

    IReadOnlyList<string> HeaderNames { get; }
}

/// <summary>
/// What every revocation case needs: one Account with a Session and a Token for the same Space, a way
/// to send as either, and the five events that end or narrow a credential.
/// </summary>
/// <remarks>
/// <para>
/// <b>Red-phase scaffold contract.</b> It lists operations; it is not a design. On activation,
/// implement it over Task 3's harness or replace it with the real types, and record which in the
/// Debug Log.
/// </para>
/// <para>
/// <b>The Session surface always sends the cookie captured at sign-in.</b> Sign-out's expiring
/// <c>Set-Cookie</c> is not applied, so the request after sign-out is the replay of the pre-sign-out
/// cookie that Lee's decision 1 says grants nothing.
/// </para>
/// <para>
/// <b>Raw SQL only where no endpoint exists yet</b> (Membership removal, Space and Account deletion,
/// Role change - their endpoints arrive in 3.3, 4.5 and 5.4), mirroring
/// <c>ActiveSpaceEndpointTests.Tokens.cs:1090-1188</c>. The constraints checked: Membership removal runs
/// under the Space's session key (RLS FILTER on <c>Membership</c>) and cascades to <c>ApiToken</c>
/// through <c>FK_ApiToken_Membership_BoundSpaceId_AccountId</c> (Cascade). Space deletion runs under the
/// Space's key so the FILTER predicate exposes every <c>Membership</c>, <c>StatusDefinition</c> and
/// <c>SpacePresence</c> row the cascades reach (<c>FK_Membership_Space_SpaceId</c>,
/// <c>FK_StatusDefinition_Space_SpaceId</c>, <c>FK_SpacePresence_Space_SpaceId</c>, all Cascade).
/// Account deletion removes the Memberships first, because <c>FK_Membership_Account_AccountId</c> is
/// Restrict, under the <c>AccountId</c> session key, then the Account, in one transaction. A Role
/// change updates one Membership under its Space's key and leaves at most one Owner per Space
/// (<c>UX_Membership_SpaceId_Owner</c>).
/// </para>
/// </remarks>
internal interface IRevocationWorld : IAsyncDisposable
{
    Guid AccountId { get; }

    Guid SpaceId { get; }

    Guid TokenId { get; }

    Task<IRevocationAnswer> SendAsync(RevocationSurface surface, HttpMethod method, string path);

    Task<IRevocationAnswer> SendWithoutCredentialAsync(HttpMethod method, string path);

    /// <summary>
    /// <c>DELETE /api/v1/sessions</c> with the Session cookie and the anti-forgery header.
    /// </summary>
    Task<IRevocationAnswer> SignOutAsync();

    /// <summary>
    /// <c>DELETE /api/v1/sessions</c> with the bearer Token alone and no anti-forgery header.
    /// </summary>
    Task<IRevocationAnswer> SignOutWithTokenAsync();

    /// <summary>
    /// <c>DELETE /api/v1/sessions</c> with no credential and no anti-forgery header.
    /// </summary>
    Task<IRevocationAnswer> SignOutWithoutCredentialAsync();

    Task RemoveMembershipAsync(bool commit);

    Task DeleteSpaceAsync();

    Task DeleteAccountAsync();

    Task ChangeRoleAsync(string roleStableCode);

    Task<DateTimeOffset?> SessionsValidAfterAsync();

    Task<IReadOnlyList<string>> RefusalKindsAsync();

    long LogCursor();

    Task<string> LogSinceAsync(long cursor);

    IReadOnlyList<string> Secrets { get; }
}
