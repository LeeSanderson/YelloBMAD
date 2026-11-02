using System.Globalization;
using Microsoft.AspNetCore.Components;
using Yello.Client.Refusals;
using Yello.Client.Spaces;
using Yello.Contracts;
using Yello.Contracts.Accounts;
using Yello.Contracts.Invitations;

namespace Yello.Client.Pages;

/// <summary>
/// The Invitation view's behaviour - see the page's own remarks for the rules. Story 4.3, AC2-AC4, AC6, AC7, AC9, AC13.
/// </summary>
public sealed partial class InvitationView
{
    private const string NotSentKey = "InvitationNotSent";

    private InvitationViewPhase _phase = InvitationViewPhase.Loading;
    private InvitationOfferForm _form = InvitationOfferForm.None;
    private InvitationWork _work = InvitationWork.None;
    private InvitationOfferResponse? _offer;
    private string? _notice;
    private string? _acceptKey;
    private int _generation;
    private bool _focusHeading;
    private ElementReference _heading;
    private IReadOnlyList<string> _failureCodes = [];

    private string _displayName = string.Empty;
    private string _emailAddress = string.Empty;
    private string _password = string.Empty;

    /// <summary>
    /// The token, exactly as the link carries it. Never stored, never sent in a request URL.
    /// </summary>
    [Parameter]
    public string Token { get; set; } = string.Empty;

    private bool IsBusy => _work != InvitationWork.None;

    /// <summary>
    /// The offer is addressed to the Account this browser is signed in as (Lee's Q5 (a)): the join action is offered.
    /// </summary>
    private bool IsAddressedToTheSession => _offer?.AddressedToYou == true;

    private string OfferHeading =>
        string.Format(CultureInfo.CurrentCulture, Localizer["InvitationOfferHeading"], _offer?.IssuerName, _offer?.SpaceName);

    private string JoinAsCopy =>
        string.Format(CultureInfo.CurrentCulture, Localizer["InvitationJoinAs"], RoleName);

    private string RoleMeaningCopy =>
        string.Format(CultureInfo.CurrentCulture, Localizer["InvitationRoleMeaning"], RoleName, Localizer[RoleCopy.PermitsKey(_offer?.Role ?? string.Empty)]);

    private string JoinCopy =>
        string.Format(CultureInfo.CurrentCulture, Localizer["InvitationJoin"], _offer?.SpaceName);

    private string RoleName => Localizer[RoleCopy.NameKey(_offer?.Role ?? string.Empty)];

    private string? StatusCopy => _work switch
    {
        InvitationWork.CreatingAccount => Localizer["InvitationCreatingAccount"],
        InvitationWork.SigningIn => Localizer["SignInInFlight"],
        InvitationWork.Joining => Localizer["InvitationJoining"],
        InvitationWork.SigningOut => null,
        _ => _notice,
    };

    /// <inheritdoc />
    protected override Task OnParametersSetAsync() => ReadAsync();

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        // Focus starts on the heading region once the server has answered (NFR-9, UX-DR39): the heading is not there
        // when the router's FocusOnNavigate looks, because nothing is rendered before the answer.
        if (_focusHeading && _phase != InvitationViewPhase.Loading)
        {
            _focusHeading = false;
            await _heading.FocusAsync();
        }
    }

    /// <summary>
    /// Reads the view. A newer read wins over an older one still on the wire (story 4.2's review lesson).
    /// </summary>
    private async Task ReadAsync()
    {
        var generation = ++_generation;

        _phase = InvitationViewPhase.Loading;
        _offer = null;

        var (phase, offer) = await Calls.ReadOfferAsync(Token);

        if (generation != _generation)
        {
            return;
        }

        _offer = offer;
        _phase = phase;
        _focusHeading = true;
    }

    private void OpenRegistration() => Open(InvitationOfferForm.Register);

    private void OpenSignIn() => Open(InvitationOfferForm.SignIn);

    private void Open(InvitationOfferForm form)
    {
        _form = form;
        _notice = null;
        _failureCodes = [];
    }

    /// <summary>
    /// The join action: the deliberate act (AC4).
    /// </summary>
    private async Task JoinAsync()
    {
        _notice = null;
        _work = InvitationWork.Joining;

        try
        {
            await AcceptAsync();
        }
        finally
        {
            _work = InvitationWork.None;
        }
    }

    /// <summary>
    /// The create-and-join action (Lee's Q3 (a)): the unchanged registration route, then sign-in with the same
    /// credentials, then the accept - one act by the person, three requests.
    /// </summary>
    private async Task RegisterAndJoinAsync()
    {
        _notice = null;
        _failureCodes = [];
        _work = InvitationWork.CreatingAccount;

        try
        {
            if (!Succeeded(await Calls.RegisterAsync(new RegisterAccountRequest(_displayName, _emailAddress, _password)))
                || !await AuthenticateAsync())
            {
                return;
            }

            _work = InvitationWork.Joining;

            await AcceptAsync();
        }
        finally
        {
            _work = InvitationWork.None;
        }
    }

    /// <summary>
    /// Signs in on the view itself (open question 16 (a)): no navigation and no return address, so the token stays in
    /// this page's URL alone. Then the view is read again, now with the Session.
    /// </summary>
    private async Task SignInAsync()
    {
        _notice = null;
        _work = InvitationWork.SigningIn;

        try
        {
            if (!await AuthenticateAsync())
            {
                return;
            }

            _form = InvitationOfferForm.None;

            await ReadAsync();
        }
        finally
        {
            _work = InvitationWork.None;
        }
    }

    /// <summary>
    /// Signs the different Account out (AC7) - on the server, through <c>SessionsValidAfter</c>, as the context bar does -
    /// and stays at this Invitation, which re-reads the offer for no Session.
    /// </summary>
    private async Task SignOutAsync()
    {
        _work = InvitationWork.SigningOut;

        try
        {
            await Calls.SignOutAsync();
        }
        finally
        {
            _work = InvitationWork.None;
        }

        await Session.SignedOutAsync(ReadAsync);
    }

    /// <summary>
    /// Sign-in with the typed credentials. On success the Session is recorded, which drops the anti-forgery token minted
    /// while anonymous - the accept fetches a fresh one.
    /// </summary>
    private async Task<bool> AuthenticateAsync()
    {
        if (!Succeeded(await Calls.AuthenticateAsync(new SignInRequest(_emailAddress, _password))))
        {
            return false;
        }

        Session.SignedIn();
        _password = string.Empty;

        return true;
    }

    private bool Succeeded(InvitationCallFailure? failure)
    {
        if (failure is null)
        {
            return true;
        }

        _notice = Localizer[failure.CopyKey];
        _failureCodes = failure.FieldCodes;

        return false;
    }

    /// <summary>
    /// The accept. A fresh key per press, kept only for an identical resend after an accept that got no answer (open
    /// question 11 (a)). Never optimistic: the page lands in the Space only after the <c>200</c>.
    /// </summary>
    private async Task AcceptAsync()
    {
        _acceptKey ??= Guid.NewGuid().ToString("D");

        var answer = await Calls.AcceptAsync(Token, _acceptKey);

        if (!answer.Answered)
        {
            _notice = Localizer[NotSentKey];
            return;
        }

        // An answer: whatever it was, the next press is a new act with a new key.
        _acceptKey = null;

        if (answer.SpaceId is { } spaceId)
        {
            Joined(spaceId);
            return;
        }

        Refused(answer.Problem);
    }

    /// <summary>
    /// The joined Space's home, with the switcher read again so it lists it, and the joined sentence said there
    /// (open question 19 (a)). The tab enters the Space first, so the purge that arriving runs has already run and the
    /// sentence is held for that Space's home alone.
    /// </summary>
    private void Joined(Guid spaceId)
    {
        var sentence = string.Format(CultureInfo.CurrentCulture, Localizer["InvitationJoined"], _offer?.SpaceName);

        Directory.Forget();
        ActiveSpace.Navigating(spaceId);
        Narration.Say(sentence);
        Navigation.GoTo(ActiveSpace, ClientRoutes.SpaceHome(spaceId));
    }

    /// <summary>
    /// A refused accept: the uniform "no longer valid" and the different-Account state replace the offer; anything else -
    /// the bound's refusal among them - is said on it.
    /// </summary>
    private void Refused(ProblemResponse? problem)
    {
        if (string.Equals(problem?.Type, ProblemTypes.InvitationNotValid, StringComparison.Ordinal))
        {
            Show(InvitationViewPhase.NotValid);
            return;
        }

        if (string.Equals(problem?.Type, ProblemTypes.InvitationForAnotherAccount, StringComparison.Ordinal))
        {
            Show(InvitationViewPhase.ForAnotherAccount);
            return;
        }

        _notice = RefusalText.Of(Localizer, problem);
    }

    private void Show(InvitationViewPhase phase)
    {
        _phase = phase;
        _focusHeading = true;
    }

    /// <summary>
    /// The localised message for the first registration failure concerning a given field, if any.
    /// </summary>
    private string? MessageFor(string field)
    {
        var code = _failureCodes.FirstOrDefault(candidate => candidate.StartsWith(field, StringComparison.Ordinal));

        return code is null ? null : Localizer[code].Value;
    }
}

/// <summary>
/// What the Invitation view shows. Story 4.3.
/// </summary>
public enum InvitationViewPhase
{
    /// <summary>
    /// Nothing Space-named until the server has answered.
    /// </summary>
    Loading,

    /// <summary>
    /// The offer: Space, Role and issuer.
    /// </summary>
    Offer,

    /// <summary>
    /// The uniform answer for a revoked, accepted, expired, lapsed or unrecognised Invitation (AC6).
    /// </summary>
    NotValid,

    /// <summary>
    /// A different Account is signed in (AC7).
    /// </summary>
    ForAnotherAccount,

    /// <summary>
    /// The view got no usable answer.
    /// </summary>
    Unavailable,
}

/// <summary>
/// Which form, if any, the offer shows to a person with no Session. Story 4.3.
/// </summary>
public enum InvitationOfferForm
{
    /// <summary>
    /// The two choices.
    /// </summary>
    None,

    /// <summary>
    /// The registration form.
    /// </summary>
    Register,

    /// <summary>
    /// The sign-in form, inline.
    /// </summary>
    SignIn,
}

/// <summary>
/// What the view is doing on the person's behalf. Story 4.3.
/// </summary>
public enum InvitationWork
{
    /// <summary>
    /// Nothing.
    /// </summary>
    None,

    /// <summary>
    /// Registering, then signing in.
    /// </summary>
    CreatingAccount,

    /// <summary>
    /// Signing in.
    /// </summary>
    SigningIn,

    /// <summary>
    /// Sending the accept.
    /// </summary>
    Joining,

    /// <summary>
    /// Signing the different Account out.
    /// </summary>
    SigningOut,
}
