namespace Yello.Application.Outbox;

/// <summary>
/// Delivers one rendered email. Consumed only by the outbox dispatcher in Infrastructure; no slice sends mail directly.
/// Story 4.1, AC11.
/// </summary>
/// <remarks>
/// The adapter is chosen by configuration: Azure Communication Services when <c>ConnectionStrings:acs</c> is set, a
/// file drop when <c>Email:DropFolder</c> is set, and otherwise the Host refuses to start.
/// </remarks>
public interface IEmailSender
{
    /// <summary>
    /// Sends the email, or throws <see cref="EmailNotSentException"/>.
    /// </summary>
    /// <param name="email">The email.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task that completes once the provider has accepted the email.</returns>
    Task SendAsync(OutboxEmail email, CancellationToken cancellationToken);
}

/// <summary>
/// An email the provider did not accept. Its message never carries the recipient, the subject or the body (NFR-6).
/// </summary>
public sealed class EmailNotSentException : Exception
{
    /// <summary>
    /// Initialises a new instance.
    /// </summary>
    public EmailNotSentException()
    {
    }

    /// <summary>
    /// Initialises a new instance.
    /// </summary>
    /// <param name="message">Why, with no recipient, subject or body in it.</param>
    public EmailNotSentException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initialises a new instance.
    /// </summary>
    /// <param name="message">Why, with no recipient, subject or body in it.</param>
    /// <param name="innerException">The transport's failure.</param>
    public EmailNotSentException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>
    /// Whether the provider refused the email for good, so trying again cannot succeed: the outbox then deletes it unsent
    /// (Lee's decision D2 (a), story 4.1 code review). <c>false</c> for anything that may pass on a later attempt.
    /// </summary>
    public bool IsPermanent { get; init; }
}
