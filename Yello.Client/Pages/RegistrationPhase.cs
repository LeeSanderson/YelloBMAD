namespace Yello.Client.Pages;

/// <summary>
/// Where a registration attempt has got to.
/// </summary>
/// <remarks>
/// <para>
/// This is the submit-and-wait state pattern <c>EXPERIENCE.md:273</c> fixes, and registration is
/// one of only two places in a product with no save button that has one. The three states below
/// are the three the pattern names: before, during, and after.
/// </para>
/// <para>
/// <b>There is no state for "that address is already registered", and there never can be.</b>
/// AD-23 makes a duplicate registration answer exactly as a new one does, so
/// <see cref="Complete"/> is what both paths reach. Adding a state here would be the client half
/// of the enumeration oracle the whole decision exists to close - and the surface has no way to
/// reach one anyway, because the server sends nothing that distinguishes them.
/// </para>
/// <para>
/// In its own file rather than in the page's <c>@code</c> block so the copy gate that scans those
/// blocks has less to read, and so the states are nameable from a test.
/// </para>
/// </remarks>
public enum RegistrationPhase
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
    /// The server answered <c>204</c>. Completion is announced - which is where AC6's obligation
    /// ends.
    /// </summary>
    /// <remarks>
    /// <b>This story does not sign the new Account in and does not navigate anywhere.</b> Story
    /// 1.4 owns authentication and Sessions; story 1.7 owns the context bar a person would arrive
    /// at. UJ-1's "lands directly in a Space already named" is the epic's outcome, realised once
    /// those two land, not this story's - confirmed by Lee on 2026-08-28.
    /// </remarks>
    Complete,
}
