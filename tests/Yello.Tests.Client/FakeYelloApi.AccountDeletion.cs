using System.Text.Json;
using Yello.Contracts;
using Yello.Contracts.Accounts;

namespace Yello.Tests.Client;

/// <summary>
/// Deleting one's own Account on the fake: <c>DELETE /api/v1/account</c>, answered as the Host answers it - <c>409
/// account-owns-spaces</c> listing <see cref="OwnedSpaces"/> as <c>{ id, name }</c> while any are set, otherwise
/// <c>204</c> with the Account signed out and every Space, Role and Token gone.
/// </summary>
/// <remarks>
/// <b>Inert unless a case sends the route</b>, so a case that never deletes an Account is served exactly what it would be
/// without this partial.
/// </remarks>
internal sealed partial class FakeYelloApi
{
    /// <summary>
    /// The Spaces the signed-in Account owns, by identity, in the order the refusal lists them. Set by a case; while any are
    /// set, the deletion is refused.
    /// </summary>
    public IList<(Guid Id, string Name)> OwnedSpaces { get; } = new List<(Guid Id, string Name)>();

    /// <summary>
    /// When set, the next deletion answers this status with <c>service-unavailable</c> and changes nothing.
    /// </summary>
    public int? NextAccountDeletionFailure { get; set; }

    /// <summary>
    /// Every Account deletion the page sent, in order.
    /// </summary>
    public IReadOnlyList<FakeRequest> AccountDeletions =>
        [.. _requests.Where(request => request.Method == "DELETE" && request.Path == AccountRoutes.Account)];

    /// <summary>
    /// The deletion's answer, or <c>null</c> when the request is not the deletion.
    /// </summary>
    private (int Status, string Body)? AccountDeletion(FakeRequest request)
    {
        if (request.Method != "DELETE" || request.Path != AccountRoutes.Account)
        {
            return null;
        }

        if (NextAccountDeletionFailure is { } failure)
        {
            NextAccountDeletionFailure = null;

            return (failure, Problem(ProblemTypes.ServiceUnavailable));
        }

        if (OwnedSpaces.Count > 0)
        {
            var refusal = new
            {
                type = ProblemTypes.AccountOwnsSpaces,
                spaces = OwnedSpaces.Select(space => new { id = space.Id, name = space.Name }).ToList(),
            };

            return (409, JsonSerializer.Serialize(refusal, JsonOptions));
        }

        SignedIn = false;
        Switcher.Clear();
        Roles.Clear();
        ApiTokens.Clear();

        return (204, string.Empty);
    }
}
