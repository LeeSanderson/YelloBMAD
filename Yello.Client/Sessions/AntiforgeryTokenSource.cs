using System.Net.Http.Json;
using System.Text.Json;
using Yello.Contracts.Accounts;

namespace Yello.Client.Sessions;

/// <summary>
/// Fetches the anti-forgery request token once and hands it to <see cref="SessionState"/>, which
/// is where <see cref="YelloApiHandler"/> reads it from. AR-14 / AD-7.
/// </summary>
/// <remarks>
/// <para>
/// <b>Lazy rather than fetched at boot.</b> A boot-time call would make the client's startup
/// depend on the Host being up - and startup is the one thing in this application that has already
/// failed silently once, on 2026-09-01, in every browser. Fetching on the first submit instead
/// means a Host that is down surfaces as the transport failure the forms already render, and a
/// retry is the person pressing the button again.
/// </para>
/// <para>
/// <b>Held in a field, never in <c>localStorage</c> or <c>sessionStorage</c>.</b> AC5 bans a
/// credential in either, and UX-DR31 extends the ban to IndexedDB for Space-scoped content. This
/// value dies with the tab, which is the correct lifetime for it: the cookie half the server paired
/// it with is a session cookie too.
/// </para>
/// </remarks>
/// <param name="http">The client, whose handler attaches the token to what follows.</param>
/// <param name="session">Where the token is held.</param>
public sealed class AntiforgeryTokenSource(HttpClient http, SessionState session)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// Makes sure a token is held, fetching one if it is not.
    /// </summary>
    /// <returns>
    /// Whether a token is now held. <c>false</c> means the request failed, and the caller reports
    /// it as the transport problem it is - rather than submitting without a token and receiving a
    /// 400 that says nothing useful to the person reading it.
    /// </returns>
    public async Task<bool> EnsureTokenAsync()
    {
        if (!string.IsNullOrEmpty(session.AntiforgeryRequestToken))
        {
            return true;
        }

        try
        {
            var token = await http
                .GetFromJsonAsync<AntiforgeryTokenResponse>(SessionRoutes.AntiforgeryToken, JsonOptions)
                .ConfigureAwait(false);

            session.HoldAntiforgeryToken(token?.RequestToken);
        }
        catch (HttpRequestException)
        {
            // The request never reached the server. Reported by the caller as a transport problem,
            // which is what it is.
            return false;
        }
        catch (JsonException)
        {
            // The server answered something this client cannot read. Same report.
            return false;
        }
        catch (OperationCanceledException)
        {
            // HttpClient's own timeout throws TaskCanceledException, which derives from this and
            // from neither of the two above - the exact gap code review found open on the
            // registration surface on 2026-09-10.
            return false;
        }

        return !string.IsNullOrEmpty(session.AntiforgeryRequestToken);
    }
}
