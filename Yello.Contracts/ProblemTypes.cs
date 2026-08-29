namespace Yello.Contracts;

/// <summary>
/// The stable <c>type</c> values Yello's RFC 9457 problem responses carry. AR-34.
/// </summary>
/// <remarks>
/// <para>
/// <b>The <c>type</c> is the contract; the <c>title</c> and <c>detail</c> beside it are not.</b>
/// A client decides what to do from the value here and renders wording from its own resources, so
/// changing the prose in a problem response can never change behaviour and a new locale needs no
/// server change.
/// </para>
/// <para>
/// <b>A URN rather than an <c>https://</c> URL.</b> RFC 9457 wants a URI, and it does not have to
/// dereference - but an <c>https://</c> type reads as a promise that a page exists there, and
/// Yello has no documentation domain. A URN is a URI, is stable, is obviously not a fetch target,
/// and claims no DNS name the project does not own.
/// </para>
/// <para>
/// In <c>Yello.Contracts</c> so the client matches on the same constant the Host emits, rather
/// than on a string literal copied into a component.
/// </para>
/// </remarks>
public static class ProblemTypes
{
    /// <summary>
    /// The registration submission was not well-formed. Carries an <c>errors</c> extension
    /// listing the failed rules by their stable codes.
    /// </summary>
    /// <remarks>
    /// <b>This never means "that address is taken".</b> A duplicate registration answers
    /// <c>204 No Content</c> exactly as a new one does (AD-23); the only thing this type reports
    /// is a malformed submission, which depends on what was sent and never on what is stored.
    /// </remarks>
    public static string InvalidRegistration => "urn:yello:problem:invalid-registration";
}
