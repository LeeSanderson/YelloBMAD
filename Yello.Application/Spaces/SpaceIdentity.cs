namespace Yello.Application.Spaces;

/// <summary>
/// A Space's identity, and nothing else: what the Space switcher may return. AD-24 / AR-29, FR-9.
/// </summary>
/// <remarks>
/// <b>Exactly two members, and a gate holds it there.</b> AD-24 lets an Account-scoped surface
/// return Space identity only - no count, no Role, no badge (story 1.7, AC2) - so
/// <c>SpaceSwitcherGateTests.The_switcher_response_and_the_Space_identity_carry_exactly_an_id_and_a_name</c>
/// fails the build on a third property here or on the wire type.
/// </remarks>
/// <param name="Id">The Space's id.</param>
/// <param name="Name">The Space's name. Not unique - display names are not.</param>
public sealed record SpaceIdentity(Guid Id, string Name);
