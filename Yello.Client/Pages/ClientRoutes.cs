using System.Globalization;

namespace Yello.Client.Pages;

/// <summary>
/// The client's own routes, stated once so the composition root, the components and the tests
/// cannot disagree about them.
/// </summary>
/// <remarks>
/// <para>
/// <b>What <c>/</c> is.</b> Story 1.3 routed registration there and called it provisional; story
/// 1.4 took it for sign-in; story 1.7 made it the <b>entry point</b>, which is what it was waiting
/// for. Signed out, it goes to sign-in. Signed in, it asks the Space switcher and lands on the first
/// Space listed, in the order served - or, for an Account holding no Membership, says so and creates
/// nothing (UX-DR35). Sign-in itself lives at <c>/sign-in</c> alone.
/// </para>
/// <para>
/// <b>Never a remembered last Space</b> (UX-DR32). Nothing records which Space was visited last:
/// the refusal page's route back is <see cref="Home"/>, and <see cref="Home"/> draws its Space from
/// the switcher's current answer. <c>SpaceSwitcherGateTests.No_client_code_remembers_a_last_Space</c>
/// holds it.
/// </para>
/// <para>
/// <b>Registration stays reachable, by a link on the sign-in surface.</b>
/// </para>
/// </remarks>
public static class ClientRoutes
{
    /// <summary>
    /// The first segment of every Space-scoped client route.
    /// </summary>
    private const string SpacesSegment = "spaces";

    /// <summary>
    /// What ends a route's path: its query, or its fragment.
    /// </summary>
    private static readonly char[] PathEnds = ['?', '#'];

    /// <summary>
    /// The sign-in surface.
    /// </summary>
    public static string SignIn => "/sign-in";

    /// <summary>
    /// The entry point: where sign-in lands and where the refusal surface's one link goes. Resolves
    /// to a Space drawn from the switcher, never to a remembered one.
    /// </summary>
    public static string Home => "/";

    /// <summary>
    /// The refusal surface, at a route of its own.
    /// </summary>
    public static string NotAvailable => "/not-available";

    /// <summary>
    /// The registration surface.
    /// </summary>
    public static string Register => "/register";

    /// <summary>
    /// The query-string key the sign-in surface reads its reason from.
    /// </summary>
    /// <remarks>
    /// A query parameter rather than client state, because the navigation that carries it destroys
    /// the component tree that would have held the state - and because it survives the reload a
    /// person may well perform next.
    /// </remarks>
    public static string ReasonQueryKey => "reason";

    /// <summary>
    /// The one reason value: the Session ended.
    /// </summary>
    /// <remarks>
    /// <b>It names the Session and nothing else.</b> UJ-6 tells a <i>live</i> participant that
    /// access ended, because that connection was already authorised for the Space; UJ-4's cold
    /// path must not, and UX-DR38 keeps the Space name out of the copy "in any case". There is
    /// deliberately only one value. A sign-out lands on <see cref="SignIn"/> with no reason at all:
    /// the person chose it, and nothing ended under them.
    /// </remarks>
    public static string SessionEndedReason => "session-ended";

    /// <summary>
    /// Where a client whose Session has ended is returned to, with the reason stated.
    /// </summary>
    public static string SignInAfterSessionEnd =>
        $"{SignIn}?{ReasonQueryKey}={SessionEndedReason}";

    /// <summary>
    /// A Space's home, the first Space-scoped route.
    /// </summary>
    /// <param name="spaceId">The Space.</param>
    /// <returns>The path.</returns>
    public static string SpaceHome(Guid spaceId) => $"/spaces/{spaceId:D}";

    /// <summary>
    /// The Space a client route is in, or <c>null</c> for a route outside every Space.
    /// </summary>
    /// <param name="relativePath">
    /// The path relative to the base address, as <c>NavigationManager.ToBaseRelativePath</c> gives
    /// it - with or without a query or fragment.
    /// </param>
    /// <returns>The Space id in the route's first two segments, if they are <c>spaces/{id}</c>.</returns>
    /// <remarks>
    /// Read as the router reads it: the segment case-insensitively, and the id in any format the
    /// <c>:guid</c> constraint accepts. A stricter reading would leave an address the router renders
    /// as a Space page looking like no Space at all to the purge hook.
    /// </remarks>
    public static Guid? SpaceIn(string? relativePath)
    {
        var path = PathOf(relativePath);
        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);

        return segments is [var first, var id, ..]
            && string.Equals(first, SpacesSegment, StringComparison.OrdinalIgnoreCase)
            && Guid.TryParse(id, CultureInfo.InvariantCulture, out var spaceId)
            ? spaceId
            : null;
    }

    /// <summary>
    /// Whether a client route is one a person reaches without a Session - sign-in or registration -
    /// where the context bar is never drawn.
    /// </summary>
    /// <param name="relativePath">The path relative to the base address.</param>
    /// <returns><c>true</c> for sign-in and registration, with or without a query, in any case.</returns>
    public static bool IsReachedWithoutASession(string? relativePath)
    {
        var path = PathOf(relativePath);

        return string.Equals(path, SignIn.TrimStart('/'), StringComparison.OrdinalIgnoreCase)
            || string.Equals(path, Register.TrimStart('/'), StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Whether a client route is the entry point, <see cref="Home"/>.
    /// </summary>
    /// <param name="relativePath">The path relative to the base address.</param>
    /// <returns><c>true</c> for <c>/</c>, with or without a query or fragment.</returns>
    public static bool IsHome(string? relativePath) => PathOf(relativePath).Length == 0;

    private static string PathOf(string? relativePath) =>
        (relativePath ?? string.Empty).Split(PathEnds)[0].Trim('/');
}
