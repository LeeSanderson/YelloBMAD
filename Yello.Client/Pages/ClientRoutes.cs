namespace Yello.Client.Pages;

/// <summary>
/// The client's own routes, stated once so the composition root, the components and the tests
/// cannot disagree about them.
/// </summary>
/// <remarks>
/// <para>
/// <b>What <c>/</c> is, and the decision behind it.</b> Story 1.3 routed registration at both
/// <c>/</c> and <c>/register</c> and recorded the root as provisional: "stories 1.4 and 1.7 decide
/// what belongs there once a Session and a context bar exist". This story is half of that
/// decision, and it takes <c>/</c> for <b>sign-in</b>: after the first week, returning is the
/// common case, and a product whose landing page asks a returning person to register is asking the
/// wrong question. Story 1.7 repoints <c>/</c> at the Space home once a context bar exists, which
/// is the other half.
/// </para>
/// <para>
/// <b>Registration stays reachable, by a link on the sign-in surface.</b> Moving <c>/</c> without
/// one would leave UJ-1 - the journey the whole epic exists for - reachable only by typing a path,
/// which is not a decision any document authorises and is not what "decide what <c>/</c> becomes"
/// can reasonably mean.
/// </para>
/// </remarks>
public static class ClientRoutes
{
    /// <summary>
    /// The sign-in surface, which is also the application's root.
    /// </summary>
    public static string SignIn => "/sign-in";

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
    /// The one reason value this story defines: the Session ended.
    /// </summary>
    /// <remarks>
    /// <b>It names the Session and nothing else.</b> UJ-6 tells a <i>live</i> participant that
    /// access ended, because that connection was already authorised for the Space; UJ-4's cold
    /// path must not, and UX-DR38 keeps the Space name out of the copy "in any case". An expired
    /// Session is the cold case - so there is no Space name here, no Membership information, and
    /// no "you were removed". There is deliberately only one value: a second would be a vocabulary
    /// in which those distinctions could be drawn.
    /// </remarks>
    public static string SessionEndedReason => "session-ended";

    /// <summary>
    /// Where a client whose Session has ended is returned to, with the reason stated.
    /// </summary>
    public static string SignInAfterSessionEnd =>
        $"{SignIn}?{ReasonQueryKey}={SessionEndedReason}";
}
