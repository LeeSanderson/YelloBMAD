namespace Yello.Contracts.Accounts;

/// <summary>
/// The registration request body, shared by the client that sends it and the Host that reads it.
/// </summary>
/// <remarks>
/// <para>
/// <b>There is no response DTO, deliberately.</b> <c>POST /api/v1/accounts</c> answers
/// <c>204 No Content</c> whether the address was new or already registered - Lee's decision of
/// 2026-08-28 - and with no body there is nothing that can differ between the two paths, nothing
/// to keep in step, and no identifier to leak. A <c>201 Created</c> would have forced the
/// duplicate path to fabricate an id, which is either a leak or a lie;
/// <c>ARCHITECTURE-SPINE.md:215</c> names a <c>409 Conflict</c> here as "the exact defect AD-23
/// exists to prevent".
/// </para>
/// <para>
/// <c>YelloBMAD-handoff.md:84</c> warns that retrofitting this changes the endpoint's shape, which
/// is why the status was settled before the contract rather than after it.
/// </para>
/// <para>
/// This type is compiled into the WebAssembly payload as well as the server, so it carries no
/// validation attributes and no framework types: <c>Yello.Contracts</c> references nothing, and
/// the per-ring package ban keeps ASP.NET Core and EF Core out of it entirely.
/// </para>
/// </remarks>
/// <param name="DisplayName">The name the person is shown by, and the Space is named from.</param>
/// <param name="EmailAddress">The address the Account is unique by.</param>
/// <param name="Password">The password. Sent once, never returned, never logged.</param>
public sealed record RegisterAccountRequest(
    string DisplayName,
    string EmailAddress,
    string Password);
