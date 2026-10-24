namespace Yello.Tests.Isolation;

/// <summary>
/// The entry point of the scaffold contract. Throws until story 1.9 Task 3 gives it a harness.
/// </summary>
internal static class IsolationWorld
{
    public static Task<IIsolationWorld> StartAsync(IsolationWorldOptions options = default) =>
        throw new InvalidOperationException(
            "The isolation harness does not exist yet (story 1.9 Task 2: one engine per test process; Task 3: RunningHost, "
            + $"surface actor, product seeding). Options requested: {options}.");

    public static readonly ScaffoldSurface[] Surfaces = Enum.GetValues<ScaffoldSurface>();
}

/// <summary>
/// The two surfaces every isolation case runs on (AD-4): what the browser sends - the Session cookie
/// and the anti-forgery header - and what a script sends, a bearer API Token and nothing else.
/// </summary>
/// <remarks>
/// <b>Red-phase scaffold contract, not the story's type.</b> Story 1.9 Task 3 names the real one
/// <c>IsolationSurface</c>; when it exists, the scaffolds in this suite use it and this enum is
/// deleted. Story 7.3 adds <c>/sync</c> as a third member (Q7), and gap gate 1 then demands an
/// expectation for it on every row.
/// </remarks>
internal enum ScaffoldSurface
{
    Session,
    ApiToken,
}

/// <summary>
/// One of the two Accounts every isolation case is about, registered through the product with its
/// own Personal Space, signed in, and holding one Token for that Space.
/// </summary>
internal interface IIsolationActor
{
    Guid AccountId { get; }

    Guid SpaceId { get; }

    string SpaceName { get; }

    string Password { get; }

    string TokenPlaintext { get; }

    Guid TokenId { get; }

    string SessionCookieValue { get; }
}

/// <summary>
/// One wire answer, as the refusal comparisons read it: status, content type, body and the header
/// names (<c>Date</c> excluded), as <c>WireResponse</c> already records them.
/// </summary>
internal interface IIsolationAnswer
{
    int Status { get; }

    string? ContentType { get; }

    string Body { get; }

    IReadOnlyList<string> HeaderNames { get; }
}

/// <summary>
/// What every isolation case needs from its harness: a real Host on a real database, two Accounts
/// seeded through the product, a way to send as either surface, the refusal record, and the Host's
/// log for the case alone.
/// </summary>
/// <remarks>
/// <para>
/// <b>Red-phase scaffold contract.</b> This lists operations, it is not a design. Story 1.9 Task 3
/// builds the harness (<c>RunningHost</c> with a log cursor, a surface actor, product seeding, raw
/// SQL only for what no endpoint does). On activation, implement this interface over that harness,
/// or replace it with the real types and correct the scaffolds. Either way, record which in the
/// Debug Log.
/// </para>
/// <para>
/// <b>Seeding is through the product</b> (test-design B2): <c>POST /api/v1/accounts</c> provisions
/// each Personal Space, so every Membership satisfies <c>FK_Membership_Account_AccountId</c>
/// (Restrict), <c>FK_Membership_Space_SpaceId</c> (Cascade), <c>UX_Membership_SpaceId_Owner</c> and
/// <c>IX_Membership_SpaceId_AccountId</c> by construction; every Token is issued through
/// <c>POST /spaces/{id}/api-tokens</c>, so <c>FK_ApiToken_Membership_BoundSpaceId_AccountId</c> holds
/// by construction. The one raw-SQL seed, <see cref="AddSecondOwnedSpaceAsync"/>, writes the
/// <c>Space</c> before its Owner <c>Membership</c>, under that Space's session key (RLS BLOCK AFTER
/// INSERT), for an Account that already exists.
/// </para>
/// </remarks>
internal interface IIsolationWorld : IAsyncDisposable
{
    IIsolationActor A { get; }

    IIsolationActor B { get; }

    /// <summary>
    /// Sends as <paramref name="actor"/> on <paramref name="surface"/>: Session is cookie plus
    /// anti-forgery header, exactly as the browser sends; ApiToken is the bearer alone, no cookie and
    /// no anti-forgery header.
    /// </summary>
    Task<IIsolationAnswer> SendAsync(IIsolationActor actor, ScaffoldSurface surface, HttpMethod method, string path);

    /// <summary>
    /// The same request with no credential at all - the anchor <c>401</c>.
    /// </summary>
    Task<IIsolationAnswer> SendWithoutCredentialAsync(HttpMethod method, string path);

    /// <summary>
    /// The <c>Kind</c> of every <c>AccessRefusal</c> row for (<paramref name="accountId"/>,
    /// <paramref name="targetSpaceId"/>), oldest first. <c>AccessRefusal</c> carries no policy.
    /// </summary>
    Task<IReadOnlyList<string>> RefusalKindsAsync(Guid accountId, Guid targetSpaceId);

    /// <summary>
    /// The number of <c>ApiToken</c> rows for (<paramref name="accountId"/>,
    /// <paramref name="boundSpaceId"/>), read under that Space's session key.
    /// </summary>
    Task<int> TokenCountAsync(Guid accountId, Guid boundSpaceId);

    /// <summary>
    /// A second Space owned by <paramref name="actor"/>, written by raw SQL (no endpoint creates a
    /// Space yet): the <c>Space</c> row, then the Owner <c>Membership</c>, under that Space's key.
    /// </summary>
    Task<Guid> AddSecondOwnedSpaceAsync(IIsolationActor actor);

    /// <summary>
    /// A position in the Host's output. Everything written after it belongs to the case that took it.
    /// </summary>
    long LogCursor();

    /// <summary>
    /// The Host's output since <paramref name="cursor"/>, once the Host has written the line for every
    /// request sent so far.
    /// </summary>
    Task<string> LogSinceAsync(long cursor);

    /// <summary>
    /// Every secret the class holds: both passwords, every Token plaintext issued, both session-cookie
    /// values, both Space names.
    /// </summary>
    IReadOnlyList<string> Secrets { get; }

    /// <summary>
    /// The Host's own duration for the last request it finished on this world (Kestrel's
    /// request-finished line, Q5's recommendation), in milliseconds.
    /// </summary>
    Task<double> LastServerDurationMillisecondsAsync();

    /// <summary>
    /// The real Host's route table: every endpoint's HTTP methods and raw route pattern (Q4 (a)).
    /// </summary>
    Task<IReadOnlyList<(string Method, string Pattern)>> MappedRoutesAsync();

    /// <summary>
    /// The physical sessions the Host holds on this world's database, counted from a side connection
    /// through <c>sys.dm_exec_sessions</c> by <c>database_id</c> and the Host's <c>program_name</c>.
    /// </summary>
    Task<int> HostSessionCountAsync();

    /// <summary>
    /// Rows of a catalogue query on this world's database, each column as text.
    /// </summary>
    Task<IReadOnlyList<string[]>> QueryCatalogAsync(string sql);
}

/// <summary>
/// How a world's Host is started.
/// </summary>
/// <param name="MaxPoolSize">When set, the Host's connection string carries <c>Max Pool Size</c>.</param>
internal readonly record struct IsolationWorldOptions(int? MaxPoolSize);
