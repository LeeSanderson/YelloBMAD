using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Yello.Infrastructure.Persistence;

/// <summary>
/// Builds a <see cref="YelloDbContext"/> for <c>dotnet ef</c> at design time only.
/// </summary>
/// <remarks>
/// <para>
/// <c>dotnet ef migrations add</c> needs to construct the context to read the model, and this is
/// a class library with no host to ask. Without this factory the tool falls back to looking for a
/// startup project, which would make scaffolding a migration depend on <c>Yello.Host</c>'s
/// configuration - and on an environment where an Aspire-injected connection string happens to
/// be present.
/// </para>
/// <para>
/// <b>It connects to nothing, and that is correct rather than a shortcut.</b> Scaffolding a
/// migration reads the model and the migrations history in the assembly; it opens no connection.
/// <c>UseSqlServer()</c> with no connection string gives the tool the provider it needs to
/// translate the model into SQL Server DDL, and nothing more. A real connection string here would
/// be a credential in source and a second source of truth for a value
/// <c>Directory.Build.props</c> already owns.
/// </para>
/// <para>
/// <b>It is not a runtime path.</b> <c>IDesignTimeDbContextFactory&lt;T&gt;</c> is discovered by
/// the EF tooling by reflection and by nothing else; the composition root registers the context
/// through <see cref="InfrastructureServices"/>. AR-36 still holds either way - no migration is
/// applied at startup, by this class or any other.
/// </para>
/// </remarks>
public sealed class YelloDbContextFactory : IDesignTimeDbContextFactory<YelloDbContext>
{
    /// <inheritdoc />
    public YelloDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<YelloDbContext>().UseSqlServer().Options);
}
