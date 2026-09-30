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

    /// <summary>
    /// The sign-in submission was not well-formed. Carries an <c>errors</c> extension listing the
    /// failed rules by their stable codes.
    /// </summary>
    /// <remarks>
    /// <b>This never means "those credentials are wrong".</b> It reports a submission that is not
    /// an authentication attempt at all - a missing field, or a value longer than any stored one
    /// could be - and every one of its codes depends only on what was sent. A wrong password and an
    /// unknown address both answer <see cref="AuthenticationFailed"/>, identically.
    /// </remarks>
    public static string InvalidSignIn => "urn:yello:problem:invalid-sign-in";

    /// <summary>
    /// Authentication did not succeed. <b>One type for every cause</b>, carried by both the
    /// unknown-address and the wrong-password paths, and by a request made with a Session that has
    /// expired.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>This constant is AD-23 on the wire, and its single-valued-ness is the contract.</b> AC4
    /// requires the two failure causes to be identical "in status, body, shape and duration", and
    /// the surest way to keep a client from branching on the cause is to send it nothing to branch
    /// on. There is deliberately no <c>errors</c> extension on this response and no second type
    /// beside it: a client that wanted to say "no account found" could not discover it here.
    /// </para>
    /// <para>
    /// The wording a person reads is one string for every cause - "Email or password is
    /// incorrect." (<c>EXPERIENCE.md:272</c>) - resolved from a resource on the client, keyed on
    /// this type.
    /// </para>
    /// </remarks>
    public static string AuthenticationFailed => "urn:yello:problem:authentication-failed";

    /// <summary>
    /// A state-changing request arrived without a valid anti-forgery token. AR-14 / AD-7.
    /// </summary>
    /// <remarks>
    /// <b>A <c>400</c>, not a <c>403</c>.</b> AD-3 draws the 403/404 line at the Space boundary
    /// "and nowhere else", and this request never reached one - it is malformed in the sense that
    /// matters, being a request the caller did not assemble correctly. Nothing is recorded in the
    /// audit store for it either: AD-20 scopes <c>AccessRefusal</c> to "every 403 and every
    /// Space-boundary 404", with exactly two kinds, and this is neither.
    /// </remarks>
    public static string AntiforgeryTokenMissing => "urn:yello:problem:antiforgery-token-missing";

    /// <summary>
    /// The request named a Space it cannot act in. <b>One type for both causes</b>: a Space that
    /// does not exist and a Space the caller holds no Membership in. AD-3.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>A <c>404</c>, never a <c>403</c>, and never distinguished.</b> AD-1
    /// (<c>ARCHITECTURE-SPINE.md:75</c>): "A request whose Space context cannot be resolved - no
    /// such Space, or no Membership in it - is refused with 404 (AD-3), never defaulted and never
    /// distinguished." Answering differently for the two would tell a caller which Space ids exist.
    /// </para>
    /// <para>
    /// Story 1.5 refuses with this type; story 1.6 owns the refusal's full contract - the identical
    /// bodies across the two causes, their timing, and the <c>AccessRefusal</c> row every
    /// Space-boundary 404 records.
    /// </para>
    /// </remarks>
    public static string SpaceNotFound => "urn:yello:problem:space-not-found";
}
