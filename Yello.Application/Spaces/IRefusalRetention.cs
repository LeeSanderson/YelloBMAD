namespace Yello.Application.Spaces;

/// <summary>
/// Purges <c>AccessRefusal</c> records past their retention, at most once a day, when request
/// traffic gives it the chance. AD-20 / AR-11; AD-10 / AR-33.
/// </summary>
/// <remarks>
/// <b>Triggered by requests, never by a schedule.</b> "No component queries the database on a fixed
/// interval" (AD-10), and story 1.10 adds "so an idle database is never woken". A purge that runs
/// only when a request has already arrived cannot wake anything. There is no timer, no
/// <c>BackgroundService</c> and no <c>IHostedService</c> in production code; a gate fails the build
/// on one.
/// </remarks>
public interface IRefusalRetention
{
    /// <summary>
    /// Purges expired refusal records if a purge is due in this process, and returns at once if not.
    /// </summary>
    /// <param name="cancellationToken">Cancels the purge.</param>
    /// <returns>A task that completes once any purge this call performed has finished.</returns>
    Task PurgeIfDueAsync(CancellationToken cancellationToken);
}
