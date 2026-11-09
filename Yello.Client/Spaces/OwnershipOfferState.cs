using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Yello.Contracts.Spaces;

namespace Yello.Client.Spaces;

/// <summary>
/// The Ownership Offer naming this Account in the active Space, as the server last answered it - what the context bar's
/// Offer indicator renders from, and nothing else.
/// </summary>
/// <remarks>
/// <para>
/// <b>Read on every answer this tab gets about it, and never on a timer</b>: on every arrival in a Space (a first arrival, a
/// re-arrival, a re-read, a switch), when the offer dialog opens, and from the acceptance's or decline's own answer. An
/// offer answered or revoked elsewhere is seen at the next of these; nothing polls.
/// </para>
/// <para>
/// <b>Space-scoped</b>: registered with the Space purge, so nothing carries over a switch, a sign-out or a Session's end, and
/// an answer about a Space the tab has left is dropped.
/// </para>
/// </remarks>
public sealed class OwnershipOfferState : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly ActiveSpaceState _activeSpace;

    private readonly HttpClient _http;

    private readonly IDisposable _purge;

    private int _arrivalsSeen;

    /// <summary>
    /// Initializes a new instance of the <see cref="OwnershipOfferState"/> class.
    /// </summary>
    /// <param name="activeSpace">The Space this tab is in, whose arrivals trigger a read.</param>
    /// <param name="http">Reads the offer.</param>
    public OwnershipOfferState(ActiveSpaceState activeSpace, HttpClient http)
    {
        ArgumentNullException.ThrowIfNull(activeSpace);

        _activeSpace = activeSpace;
        _http = http;
        _arrivalsSeen = activeSpace.Arrivals;
        _purge = activeSpace.RegisterPurge(Forget);
        activeSpace.Changed += OnActiveSpaceChanged;
    }

    /// <summary>
    /// Raised whenever <see cref="Offer"/> changes.
    /// </summary>
    public event EventHandler? Changed;

    /// <summary>
    /// The actionable offer naming this Account in the active Space, or <c>null</c> when the last answer was that none does.
    /// </summary>
    public OwnershipOfferForRecipientResponse? Offer { get; private set; }

    /// <summary>
    /// The Space <see cref="Offer"/> is about.
    /// </summary>
    public Guid? SpaceId { get; private set; }

    /// <summary>
    /// Asks the server for the offer naming this Account in the active Space.
    /// </summary>
    /// <returns><c>true</c> when the server answered, whatever it said; <c>false</c> when it did not, or the tab left.</returns>
    public async Task<bool> RefreshAsync()
    {
        if (_activeSpace.SpaceId is not { } spaceId || _activeSpace.Current is null)
        {
            return false;
        }

        var token = _activeSpace.Token;

        try
        {
            using var response = await _http.GetAsync(new Uri(OwnershipOfferRoutes.MineOf(spaceId), UriKind.Relative), token);

            if (token.IsCancellationRequested || _activeSpace.SpaceId != spaceId)
            {
                return false;
            }

            if (response.StatusCode == HttpStatusCode.OK)
            {
                Set(spaceId, await response.Content.ReadFromJsonAsync<OwnershipOfferForRecipientResponse>(JsonOptions, token));
                return true;
            }

            // A 204 is the answer that no offer names this Account. A refusal of the Space itself is the page's to render,
            // and either way there is nothing here to offer.
            Set(spaceId, null);

            return response.StatusCode == HttpStatusCode.NoContent;
        }
        catch (Exception exception) when (exception is OperationCanceledException or HttpRequestException or JsonException)
        {
            return false;
        }
    }

    /// <summary>
    /// The offer is no longer open to this Account - it was answered, or the server said it closed - so the indicator goes.
    /// </summary>
    public void Closed()
    {
        if (Offer is null)
        {
            return;
        }

        Offer = null;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _activeSpace.Changed -= OnActiveSpaceChanged;
        _purge.Dispose();
    }

    private void Set(Guid spaceId, OwnershipOfferForRecipientResponse? offer)
    {
        SpaceId = spaceId;
        Offer = offer;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void Forget()
    {
        SpaceId = null;
        Offer = null;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void OnActiveSpaceChanged(object? sender, EventArgs args)
    {
        if (_activeSpace.Arrivals == _arrivalsSeen)
        {
            return;
        }

        _arrivalsSeen = _activeSpace.Arrivals;
        _ = RefreshAsync();
    }
}
