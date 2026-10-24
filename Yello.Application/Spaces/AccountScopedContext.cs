namespace Yello.Application.Spaces;

/// <summary>
/// The context one of AD-24's two Account-scoped surfaces runs under: an Account, and no Space.
/// AD-24 / AR-29.
/// </summary>
/// <remarks>
/// <para>
/// <b>Built by the request pipeline from the Session, never by a slice and never from a client
/// value</b> (AR-3; AD-2's "never from a client-supplied value"). The Account is the one the server
/// issued the Session to. A slice receives this; it never reads a principal itself.
/// </para>
/// <para>
/// <b>No Role and no Space, by construction.</b> An Account-scoped surface may return Space identity
/// - an id and a name - and nothing else, so there is nothing here a Role or a Space-scoped answer
/// could be derived from.
/// </para>
/// </remarks>
/// <param name="AccountId">The Account the Session identifies.</param>
public sealed record AccountScopedContext(Guid AccountId);
