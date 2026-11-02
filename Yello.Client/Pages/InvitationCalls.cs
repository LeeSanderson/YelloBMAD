using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Yello.Client.Sessions;
using Yello.Client.Spaces;
using Yello.Contracts;
using Yello.Contracts.Accounts;
using Yello.Contracts.Invitations;

namespace Yello.Client.Pages;

/// <summary>
/// The requests the Invitation view sends, each reduced to what the page shows. Story 4.3, AC2-AC4, AC6, AC7, AC9, AC13;
/// Lee's Q3 (a), Q4 (a).
/// </summary>
/// <remarks>
/// <para>
/// <b>The token never travels in a request URL</b> (Lee's Q4 (a)): the view carries it in the
/// <c>Yello-Invitation-Token</c> header and the accept in its body. Nothing here stores it.
/// </para>
/// <para>
/// <b>Registration and sign-in are the existing routes, unchanged</b> (Lee's Q3 (a); AD-22, AD-23): the same request
/// bodies <c>Register.razor</c> and <c>SignIn.razor</c> send, and the same reading of their refusals. The accept goes
/// through <see cref="SpaceWrites"/>, so its <c>Idempotency-Key</c> and anti-forgery rules are the ones every write
/// follows - including fetching a fresh anti-forgery token after sign-in, since one minted while anonymous is refused once
/// a Session exists.
/// </para>
/// </remarks>
/// <param name="http">The API client.</param>
/// <param name="antiforgery">The anti-forgery token source.</param>
/// <param name="writes">The one sender of keyed writes.</param>
public sealed class InvitationCalls(HttpClient http, AntiforgeryTokenSource antiforgery, SpaceWrites writes)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// Reads the view.
    /// </summary>
    /// <param name="token">The token from the page's URL.</param>
    /// <returns>What to show, and the offer when there is one.</returns>
    public async Task<(InvitationViewPhase Phase, InvitationOfferResponse? Offer)> ReadOfferAsync(string token)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(InvitationRoutes.Offer, UriKind.Relative));

            request.Headers.TryAddWithoutValidation(InvitationRoutes.TokenHeader, token);

            using var response = await http.SendAsync(request, CancellationToken.None);

            if (response.StatusCode == HttpStatusCode.OK
                && await response.Content.ReadFromJsonAsync<InvitationOfferResponse>(JsonOptions, CancellationToken.None) is { } offer)
            {
                return (offer.AddressedToYou == false ? InvitationViewPhase.ForAnotherAccount : InvitationViewPhase.Offer, offer);
            }

            return (response.StatusCode == HttpStatusCode.NotFound ? InvitationViewPhase.NotValid : InvitationViewPhase.Unavailable, null);
        }
        catch (Exception exception) when (IsTransport(exception))
        {
            return (InvitationViewPhase.Unavailable, null);
        }
    }

    /// <summary>
    /// Sends the accept under <paramref name="key"/>.
    /// </summary>
    /// <param name="token">The token from the page's URL.</param>
    /// <param name="key">The press's <c>Idempotency-Key</c>.</param>
    /// <returns>What came back: the joined Space, a refusal, or no answer at all.</returns>
    public async Task<InvitationAcceptAnswer> AcceptAsync(string token, string key)
    {
        try
        {
            using var answer = await writes.SendAsync(HttpMethod.Post, InvitationRoutes.Acceptance, new AcceptInvitationRequest(token), key, CancellationToken.None);

            if (answer.StatusCode >= HttpStatusCode.InternalServerError)
            {
                return InvitationAcceptAnswer.Unanswered;
            }

            if (answer.StatusCode == HttpStatusCode.OK
                && await answer.Response.Content.ReadFromJsonAsync<AcceptedInvitationResponse>(JsonOptions, CancellationToken.None) is { } accepted)
            {
                return new InvitationAcceptAnswer(accepted.SpaceId, Problem: null, Answered: true);
            }

            return new InvitationAcceptAnswer(SpaceId: null, answer.Problem, Answered: true);
        }
        catch (Exception exception) when (IsTransport(exception))
        {
            return InvitationAcceptAnswer.Unanswered;
        }
    }

    /// <summary>
    /// Registers through the unchanged route.
    /// </summary>
    /// <param name="request">The three fields, as typed.</param>
    /// <returns><c>null</c> on success; otherwise the copy key to say and the field codes refused.</returns>
    public Task<InvitationCallFailure?> RegisterAsync(RegisterAccountRequest request) =>
        PostAsync(AccountRoutes.Register, request, refusedCredentialsKey: null);

    /// <summary>
    /// Signs in through the unchanged route. An address already registered meets sign-in's ordinary failure (AD-23).
    /// </summary>
    /// <param name="request">The two fields, as typed.</param>
    /// <returns><c>null</c> on success; otherwise the copy key to say.</returns>
    public Task<InvitationCallFailure?> AuthenticateAsync(SignInRequest request) =>
        PostAsync(SessionRoutes.Authenticate, request, refusedCredentialsKey: "SignInFailed");

    /// <summary>
    /// Ends the Session on the server, as the context bar does - retried once with a fresh anti-forgery token. A sign-out
    /// the server never heard of still purges this client.
    /// </summary>
    /// <returns>A task.</returns>
    public async Task SignOutAsync()
    {
        try
        {
            if (await SendSignOutAsync() == HttpStatusCode.BadRequest)
            {
                antiforgery.Invalidate();
                await SendSignOutAsync();
            }
        }
        catch (Exception exception) when (IsTransport(exception))
        {
            // The person asked to leave: the caller purges this client whatever the server heard.
        }
    }

    private async Task<HttpStatusCode?> SendSignOutAsync()
    {
        if (!await antiforgery.EnsureTokenAsync())
        {
            return null;
        }

        using var response = await http.DeleteAsync(new Uri(SessionRoutes.Authenticate, UriKind.Relative), CancellationToken.None);

        return response.StatusCode;
    }

    private async Task<InvitationCallFailure?> PostAsync<TRequest>(string path, TRequest request, string? refusedCredentialsKey)
    {
        try
        {
            if (!await antiforgery.EnsureTokenAsync())
            {
                return InvitationCallFailure.NotSent;
            }

            using var response = await http.PostAsJsonAsync(path, request, JsonOptions, CancellationToken.None);

            if (response.IsSuccessStatusCode)
            {
                return null;
            }

            return FailureOf(await SpaceWrites.ProblemOfAsync(response, CancellationToken.None), refusedCredentialsKey);
        }
        catch (Exception exception) when (IsTransport(exception))
        {
            return InvitationCallFailure.NotSent;
        }
    }

    /// <summary>
    /// A refused registration or sign-in, read as <c>Register.razor</c> and <c>SignIn.razor</c> read it: a refused
    /// anti-forgery token is dropped and recoverable; a failed sign-in is one string for every cause; field codes are the
    /// fields' own messages.
    /// </summary>
    private InvitationCallFailure FailureOf(ProblemResponse? problem, string? refusedCredentialsKey)
    {
        if (string.Equals(problem?.Type, ProblemTypes.AntiforgeryTokenMissing, StringComparison.Ordinal))
        {
            antiforgery.Invalidate();

            return new InvitationCallFailure("SubmitRetry", []);
        }

        if (refusedCredentialsKey is not null && string.Equals(problem?.Type, ProblemTypes.AuthenticationFailed, StringComparison.Ordinal))
        {
            return new InvitationCallFailure(refusedCredentialsKey, []);
        }

        var codes = problem?.Errors ?? [];

        return codes.Count > 0 ? new InvitationCallFailure("ErrorHeading", codes) : InvitationCallFailure.NotSent;
    }

    private static bool IsTransport(Exception exception) =>
        exception is HttpRequestException or JsonException or OperationCanceledException;
}

/// <summary>
/// What an accept came to, as the Invitation view reads it.
/// </summary>
/// <param name="SpaceId">The joined Space, on a <c>200</c>.</param>
/// <param name="Problem">The refusal, when it was one.</param>
/// <param name="Answered">Whether the server answered at all (a <c>5xx</c> or a lost connection is no answer).</param>
public sealed record InvitationAcceptAnswer(Guid? SpaceId, ProblemResponse? Problem, bool Answered)
{
    /// <summary>
    /// No answer: the same press may be sent again under the same key.
    /// </summary>
    public static InvitationAcceptAnswer Unanswered { get; } = new(SpaceId: null, Problem: null, Answered: false);
}

/// <summary>
/// A refused registration or sign-in, as the Invitation view says it.
/// </summary>
/// <param name="CopyKey">The copy to say.</param>
/// <param name="FieldCodes">The registration field codes refused, for the fields' own messages.</param>
public sealed record InvitationCallFailure(string CopyKey, IReadOnlyList<string> FieldCodes)
{
    /// <summary>
    /// The request did not get through.
    /// </summary>
    public static InvitationCallFailure NotSent { get; } = new("SubmitFailed", []);
}
