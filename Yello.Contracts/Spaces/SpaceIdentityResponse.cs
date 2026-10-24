namespace Yello.Contracts.Spaces;

/// <summary>
/// One row of the Space switcher: a Space's id and its name, and nothing else. FR-9; AD-24, I-11.
/// </summary>
/// <remarks>
/// <b>Exactly two members, forever.</b> AD-24 lets an Account-scoped surface return Space identity
/// only - no count, no Role, no badge (story 1.7, AC2) - and
/// <c>SpaceSwitcherGateTests.The_switcher_response_and_the_Space_identity_carry_exactly_an_id_and_a_name</c>
/// fails the build on a third. The wire tests read the raw JSON property set, because a
/// deserialised DTO would silently drop a property it does not declare.
/// </remarks>
/// <param name="Id">The Space's id - what every Space-scoped route is called with.</param>
/// <param name="Name">The Space's name. Not unique.</param>
public sealed record SpaceIdentityResponse(Guid Id, string Name);
