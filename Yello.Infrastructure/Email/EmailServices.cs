using Microsoft.Extensions.DependencyInjection;
using Yello.Application.Outbox;
using Yello.Domain.Accounts;
using Yello.Infrastructure.Outbox;

namespace Yello.Infrastructure.Email;

/// <summary>
/// Registers the outbox and the email sender. Story 4.1, AC6, AC11; Q13 (a).
/// </summary>
public static class EmailServices
{
    /// <summary>
    /// The configuration key of the file-drop folder. Never set for a deployed environment - a gate holds it.
    /// </summary>
    public static string DropFolderConfigurationKey => "Email:DropFolder";

    /// <summary>
    /// The configuration key of the sender address (plain configuration, not a secret).
    /// </summary>
    public static string SenderConfigurationKey => "Email:Sender";

    /// <summary>
    /// The connection-string name of the Communication Services resource (<c>ConnectionStrings:acs</c>).
    /// </summary>
    public static string AcsConnectionStringName => "acs";

    /// <summary>
    /// The variable Azure Container Apps sets on every replica it runs. Its presence marks a deployed Host, where the file
    /// drop is refused at startup whatever else is configured (Lee's decision D1 (a), story 4.1 code review, 2026-11-01).
    /// </summary>
    public static string ContainerAppNameVariable => "CONTAINER_APP_NAME";

    /// <summary>
    /// Chooses and registers the email sender by configuration, or refuses to start.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The choice</b>: with <c>ConnectionStrings:acs</c>, the Azure Communication Services adapter; otherwise, with
    /// <c>Email:DropFolder</c>, the file drop (in any environment but Azure Container Apps - Q13 (a), narrowed by Lee's
    /// D1 (a)); otherwise the Host <b>fails loudly</b> rather than silently discarding mail. Either sender needs a plausible
    /// <c>Email:Sender</c>.
    /// </para>
    /// <para>
    /// <b>Never a file drop in a deployed Host.</b> When <see cref="ContainerAppNameVariable"/> is set, a configured
    /// <c>Email:DropFolder</c> stops the Host - even beside an ACS connection string - so a setting added by hand on a
    /// revision, in the base <c>appsettings.json</c> or through <c>ASPNETCORE_ENVIRONMENT=Development</c> can never put
    /// invitation tokens on a container's disk. An Architecture gate holds the repository's side of the same rule.
    /// </para>
    /// <para>
    /// The answer names the sender chosen and never the connection string, the key or the folder.
    /// </para>
    /// </remarks>
    /// <param name="services">The container.</param>
    /// <param name="acsConnectionString">The Communication Services connection string, or <c>null</c>.</param>
    /// <param name="dropFolder">The file-drop folder, or <c>null</c>.</param>
    /// <param name="senderAddress">The sender address, or <c>null</c>.</param>
    /// <param name="containerAppName">The value of <see cref="ContainerAppNameVariable"/>, or <c>null</c> off Azure.</param>
    /// <returns>Which sender was chosen, for the startup log.</returns>
    /// <exception cref="InvalidOperationException">No sender is configured, or the configuration is unusable.</exception>
    public static string AddYelloEmailSender(
        this IServiceCollection services,
        string? acsConnectionString,
        string? dropFolder,
        string? senderAddress,
        string? containerAppName)
    {
        if (!string.IsNullOrWhiteSpace(containerAppName) && !string.IsNullOrWhiteSpace(dropFolder))
        {
            throw new InvalidOperationException(
                $"{DropFolderConfigurationKey} is set on a Host running in Azure Container Apps. A deployed Host never "
                + $"writes mail to disk: remove the setting and give it ConnectionStrings:{AcsConnectionStringName}.");
        }

        if (string.IsNullOrWhiteSpace(acsConnectionString) && string.IsNullOrWhiteSpace(dropFolder))
        {
            throw new InvalidOperationException(
                $"No email sender is configured. Set ConnectionStrings:{AcsConnectionStringName} (Azure Communication Services) "
                + $"or, outside a deployed environment, {DropFolderConfigurationKey}. Mail is never silently discarded.");
        }

        if (string.IsNullOrWhiteSpace(senderAddress))
        {
            throw new InvalidOperationException($"{SenderConfigurationKey} is not configured; an email needs a sender address.");
        }

        if (!EmailAddressRule.IsPlausible(senderAddress))
        {
            throw new InvalidOperationException($"{SenderConfigurationKey} is not a plausible email address.");
        }

        if (!string.IsNullOrWhiteSpace(acsConnectionString))
        {
            if (AcsConnection.TryParse(acsConnectionString, out var connection) is { } problem)
            {
                throw new InvalidOperationException($"ConnectionStrings:{AcsConnectionStringName} is unusable: {problem}.");
            }

            services.AddSingleton<IEmailSender>(provider =>
                new AcsEmailSender(connection!, senderAddress, provider.GetRequiredService<TimeProvider>()));

            return "Azure Communication Services";
        }

        services.AddSingleton<IEmailSender>(provider =>
            new FileDropEmailSender(dropFolder!, senderAddress, provider.GetRequiredService<TimeProvider>()));

        return "file drop";
    }

    /// <summary>
    /// Adds the outbox: its store on the request's unit of work, the post-commit signal, and the singleton dispatcher
    /// with its piggyback throttle.
    /// </summary>
    /// <param name="services">The container.</param>
    /// <returns>The same collection, for chaining.</returns>
    internal static IServiceCollection AddOutbox(this IServiceCollection services)
    {
        services.AddScoped<IOutbox, OutboxStore>();
        services.AddScoped<OutboxCommitSignal>();
        services.AddSingleton<OutboxSweepSchedule>();
        services.AddSingleton<IOutboxRecovery, OutboxDispatcher>();

        return services;
    }
}
