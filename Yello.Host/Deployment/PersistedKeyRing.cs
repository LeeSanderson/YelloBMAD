using System.Xml.Linq;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.AspNetCore.DataProtection.Repositories;
using Microsoft.Extensions.Options;
using Yello.Infrastructure.Persistence;

namespace Yello.Host.Deployment;

/// <summary>
/// Keeps the Data Protection key ring in the application database, so a new revision and a cold start
/// read the cookies and anti-forgery tokens the previous process issued. Story 1.10 AC12; Finding F1,
/// open question 7.
/// </summary>
/// <remarks>
/// <para>
/// <b>No package.</b> The framework's key-repository contract is in the ASP.NET Core shared framework;
/// the rows are written through <see cref="IKeyRingStore"/> in <c>Yello.Infrastructure</c>. The
/// <c>Microsoft.AspNetCore.DataProtection.EntityFrameworkCore</c> package does the same thing and was
/// not taken, because a new package needs Lee's approval and this needs a dozen lines.
/// </para>
/// <para>
/// <b>One application name.</b> The framework's default isolates key rings by content-root path,
/// which differs between a container and a developer machine. Every Yello Host shares one.
/// </para>
/// <para>
/// <b>Read lazily, on a request.</b> The ring is loaded on the first protect or unprotect of a process
/// and refreshed by the framework when it is next used after its cache expires. Nothing here is on a
/// timer.
/// </para>
/// </remarks>
internal static class PersistedKeyRing
{
    /// <summary>
    /// The application name every Yello Host protects under.
    /// </summary>
    internal const string ApplicationName = "Yello";

    /// <summary>
    /// Persists the key ring to the database.
    /// </summary>
    /// <param name="services">The container.</param>
    /// <returns>The same collection, for chaining.</returns>
    internal static IServiceCollection AddPersistedKeyRing(this IServiceCollection services)
    {
        services.AddKeyRingStore();
        services.AddSingleton<KeyRingAvailability>();

        services.AddDataProtection().SetApplicationName(ApplicationName);

        services.AddSingleton<IConfigureOptions<KeyManagementOptions>>(provider =>
            new ConfigureOptions<KeyManagementOptions>(options =>
                options.XmlRepository = new DatabaseXmlRepository(provider.GetRequiredService<IKeyRingStore>())));

        return services;
    }
}

/// <summary>
/// The framework's key repository, over <see cref="IKeyRingStore"/>.
/// </summary>
/// <remarks>
/// A database failure propagates. The framework then has no ring and does not create a key; the
/// request is answered <c>503</c> by <see cref="DatabaseAvailability"/>.
/// </remarks>
/// <param name="store">Where the keys are kept.</param>
internal sealed class DatabaseXmlRepository(IKeyRingStore store) : IXmlRepository
{
    /// <inheritdoc />
    public IReadOnlyCollection<XElement> GetAllElements() =>
        store.ReadAll().Select(xml => XElement.Parse(xml)).ToList();

    /// <inheritdoc />
    public void StoreElement(XElement element, string friendlyName)
    {
        ArgumentNullException.ThrowIfNull(element);

        store.Add(friendlyName, element.ToString(SaveOptions.DisableFormatting));
    }
}
