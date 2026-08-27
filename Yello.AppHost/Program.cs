using System.Reflection;

// The local orchestration substrate (AC4): Yello.Host, Yello.Client and a SQL Server 2025
// container, with a working connection from the Host to the container.
//
// Orchestration for the developer machine only. This is NOT deployment - CI/CD, Azure and
// migrations-as-an-explicit-step are story 1.10's, and "build gates" in story 1.1 means test
// suites that fail `dotnet build` / `dotnet test`, not a pipeline.

var builder = DistributedApplication.CreateBuilder(args);

// The image is pinned by registry, image and tag explicitly rather than left to the hosting
// integration's default tag, which tracks a different major version of the engine. AD-15's
// Latin1_General_100_BIN2 collation and the row-level security that NFR-1 rests on are
// engine behaviour, so the engine version is not incidental.
//
// The value is the assembly metadata Directory.Build.props stamps into every assembly - the
// same source SqlServerContainerFixture reads. It used to be a literal here and a second
// literal in the fixture, so the suites and local orchestration could silently run different
// engine builds with nothing in the repository changing.
//
// The tag itself still floats by cumulative update - `2025-latest` is not a digest. That
// remains an open question rather than a settled decision, but it now floats in one place.
var (registry, image, tag) = SplitImageReference(BuildConstant("Yello.SqlServerImage"));

var sqlServer = builder.AddSqlServer("sql")
    .WithImageRegistry(registry)
    .WithImage(image)
    .WithImageTag(tag);

var database = sqlServer.AddDatabase(BuildConstant("Yello.DatabaseResourceName"));

// WaitFor is orchestration-level sequencing performed by the AppHost, not a probe inside
// Yello.Host. AR-33's ban is on the application touching the database to answer a liveness
// or readiness probe, and on any component doing so on an unconditional timer; neither
// applies to the AppHost holding a process back until its dependency is accepting
// connections.
//
// Note what WaitFor does and does not guarantee: it waits on the Aspire *resource*, not on the
// `yello` catalog existing inside it. "Container up, database missing" therefore still reaches
// the Host, which logs it as a connectivity failure - see Yello.Host/Program.cs, which
// distinguishes that case in its log rather than leaving it to look like "container down".
builder.AddProject<Projects.Yello_Host>("host")
    .WithReference(database)
    .WaitFor(database);

builder.AddProject<Projects.Yello_Client>("client");

// RunAsync rather than Run (S6966): the blocking overload parks the entry-point thread for
// the whole orchestration session. Awaiting it turns this file into an async entry point,
// which costs nothing here and is the shape Yello.Host already uses.
await builder.Build().RunAsync();

// Read from this assembly's own metadata rather than from a shared type. The AppHost's project
// references are Aspire project RESOURCES, which the Aspire SDK marks
// ReferenceOutputAssembly=false, so this project cannot compile against Yello.Host even though
// the ring table permits the edge. Directory.Build.props stamps the value into every assembly,
// so reading the local copy is reading the one source.
static string BuildConstant(string key)
{
    // typeof(Program) rather than GetExecutingAssembly (S3902): top-level statements compile
    // into an implicit Program class in this assembly, so it is a compile-time handle on
    // exactly the assembly wanted.
    var value = typeof(Program).Assembly
        .GetCustomAttributes<AssemblyMetadataAttribute>()
        .FirstOrDefault(a => a.Key.Equals(key, StringComparison.Ordinal))
        ?.Value;

    return string.IsNullOrWhiteSpace(value)
        ? throw new InvalidOperationException(
            $"Assembly metadata '{key}' is missing. Directory.Build.props emits it for every " +
            "project; if it is gone, the value it carried has no source at all.")
        : value;
}

// Aspire wants the three parts separately, and the one place the image is stated states it as
// one reference. Splitting here keeps the shared value in the form a human reads and writes.
//
// Two forms are rejected rather than parsed, because in both cases the naive arithmetic produced
// a plausible wrong answer instead of an error - which is the one outcome stating the value in a
// single place was meant to make impossible:
//
//   * A DIGEST reference. `<registry>/mssql/server@sha256:<hex>` has its last colon inside the
//     digest, so the split yielded image "mssql/server@sha256" and tag "<hex>" with no throw.
//     The fixture hands the whole reference to Testcontainers, which parses digests correctly -
//     so the AppHost and the suites would have pulled different images, silently, from one
//     shared value. Pinning a digest is an open question for stories 1.5 / 2.6; when it is
//     settled, this file and SqlServerContainerFixture must be changed together, and this
//     failure is what says so.
//   * A reference with NO registry host. `library/postgres:17` split cleanly and used "library"
//     as the registry.
static (string Registry, string Image, string Tag) SplitImageReference(string reference)
{
    var firstSlash = reference.IndexOf('/', StringComparison.Ordinal);

    if (firstSlash <= 0)
    {
        throw InvalidImageReference(reference, "it names no registry");
    }

    var registry = reference[..firstSlash];

    // A registry is a host: it carries a dot, or a port, or is literally localhost.
    if (!registry.Contains('.', StringComparison.Ordinal)
        && !registry.Contains(':', StringComparison.Ordinal)
        && !registry.Equals("localhost", StringComparison.Ordinal))
    {
        throw InvalidImageReference(reference, $"'{registry}' is not a registry host");
    }

    var remainder = reference[(firstSlash + 1)..];

    if (remainder.Contains('@', StringComparison.Ordinal))
    {
        throw InvalidImageReference(reference,
            "it pins a digest, which Aspire takes separately from a tag. Update this file and " +
            "tests/Yello.Tests.Shared/SqlServerContainerFixture.cs together, deliberately");
    }

    var lastColon = remainder.LastIndexOf(':');

    if (lastColon < 0)
    {
        throw InvalidImageReference(reference, "it names no tag");
    }

    return (registry, remainder[..lastColon], remainder[(lastColon + 1)..]);
}

static InvalidOperationException InvalidImageReference(string reference, string why) =>
    new($"'{reference}' cannot be used as a registry/image:tag reference: {why}. " +
        "Directory.Build.props states the image as one string so that the fixture and this file " +
        "cannot disagree about which engine runs.");
