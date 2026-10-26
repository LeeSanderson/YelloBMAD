namespace Yello.Contracts.Labels;

/// <summary>
/// One Label, as its Space lists it and its definition answers it. FR-22; story 2.4.
/// </summary>
/// <remarks>
/// <b>No <c>SpaceId</c></b>: the Space is the route's (AD-3). <b>The colour is a palette key</b>, never a colour value
/// (open question 3); the client's token layer owns what each key renders as.
/// </remarks>
/// <param name="Id">Its id.</param>
/// <param name="Name">Its name.</param>
/// <param name="Colour">Its palette key, such as <c>rust</c>.</param>
public sealed record LabelResponse(Guid Id, string Name, string Colour);
