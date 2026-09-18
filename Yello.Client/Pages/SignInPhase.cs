namespace Yello.Client.Pages;

/// <summary>
/// Where a sign-in attempt has got to.
/// </summary>
/// <remarks>
/// <para>
/// <c>EXPERIENCE.md:273</c> names registration and sign-in as "the only submit-and-wait
/// interactions in a product with no save button", and these are the three states that pattern
/// fixes: before, during, and after. It is the same enum as
/// <see cref="RegistrationPhase"/> by design - two surfaces drawn in the same frame row behave the
/// same way - and deliberately a separate type, because merging them would make a later divergence
/// in one surface a change to both.
/// </para>
/// <para>
/// <b>There is no state for "that address is not registered", and there never can be.</b> AD-23
/// makes an unknown address and a wrong password one answer, in status, body, shape and duration,
/// and the client could not tell them apart if it wanted to: the server sends one problem
/// <c>type</c> and no <c>errors</c> extension.
/// </para>
/// <para>
/// <b><see cref="Complete"/> exists but is barely occupied.</b> A successful sign-in has nowhere
/// to go: there is no Space home until story 1.7 and no Board until epic 2. So this story states
/// completion and stops, exactly as story 1.3's registration surface does, rather than inventing a
/// destination.
/// </para>
/// </remarks>
public enum SignInPhase
{
    /// <summary>
    /// The form is being filled in. The only state the fields are editable in.
    /// </summary>
    Editing,

    /// <summary>
    /// The request is in flight. The in-flight condition is stated, and resubmission is disabled.
    /// </summary>
    Submitting,

    /// <summary>
    /// The server answered <c>204</c> and the Session cookie is set.
    /// </summary>
    Complete,
}
