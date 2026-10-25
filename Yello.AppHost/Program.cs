using System.Reflection;

// The local orchestration substrate (AC4): Yello.Host, Yello.Client and a SQL Server 2025
// container, with a working connection from the Host to the container.
//
// Orchestration for the developer machine only. This is NOT deployment - CI/CD and Azure live in
// .github/workflows and infra/ (story 1.10). It does apply the migrations as an explicit step before
// the Host starts, as the deploy does - see the `migrate` resource below.

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
// the Host, which logs it as a connectivity failure - see Yello.Host/StartupConnectivityCheck.cs,
// which distinguishes that case in its log rather than leaving it to look like "container down".
// BOTH PROJECTS RUN ON THEIR `https` LAUNCH PROFILE, AND THAT IS STORY 1.4'S DOING.
//
// The default is the FIRST profile in each launchSettings.json, which is `http` - and that was
// deliberate until now. Story 1.4's AC5 requires the Session cookie to carry `Secure`, and a
// `Secure` cookie is only reliably accepted over https: Chromium treats http://localhost as a
// secure context, Firefox and Safari do not agree in every version, and an acceptance criterion
// that holds in one browser is not one that holds. NFR-6's encryption-in-transit clause pulls the
// same way - deferred-work.md recorded, at the 2026-09-10 review, that a password crossed the
// loopback interface in cleartext under `aspire run`, and assigned the swap to story 1.10 because
// it could not be verified from a review context.
//
// Taken here instead because AC5 is this story's and an unverifiable cookie attribute is worse
// than a configuration change. WHAT IT COSTS: a trusted development certificate. If the browser
// warns about one, run `dotnet dev-certs https --trust` once.
//
// THE TEST SUITES DO DEPEND ON IT, and this comment said the opposite until code review on
// 2026-09-19. It read "Nothing in the test suites depends on this - they boot Yello.Host as a
// process on http://127.0.0.1:0", which was true when it was written and was falsified by this very
// story: HostProcess now sets ASPNETCORE_URLS to https://127.0.0.1:0, because .NET's CookieContainer
// refuses a `Secure` cookie over http and eleven of fourteen endpoint tests went down with it. So
// Kestrel needs the development certificate to bind, and a cold CI agent without
// `dotnet dev-certs https` fails the entire Slices suite at Host startup - for a reason no assertion
// names, told otherwise by the one file somebody provisioning that agent would read.
// tests/TESTING-CONVENTIONS.md states the requirement.
var client = builder.AddProject<Projects.Yello_Client>("client", launchProfileName: "https");

// THE ONE ALLOWED ORIGIN, INJECTED RATHER THAN RESTATED.
//
// AD-7 requires CORS to allow "exactly the configured client origin", and AR-37 puts configuration
// in the environment rather than in source. Taking the value from the client resource's own
// endpoint means the Host and the client cannot disagree about what the client's origin is - which
// they would the moment somebody edited one launchSettings.json and not the other, and the symptom
// would be a CORS error in a browser console pointing at neither file.
//
// The key is a literal here because this project cannot compile against Yello.Host at all: the
// Aspire SDK marks project references ReferenceOutputAssembly=false. CrossOriginPolicy states the
// same key on the other side, and CrossOriginConfigurationTests reads THIS assembly's IL for it,
// so the two spellings cannot drift apart silently.
// THE MIGRATION, AS ITS OWN STEP BEFORE THE HOST - story 1.10, question 17 (a), Finding F4.
//
// The Host never migrates (AR-36), and until story 1.10 nothing else did here either: `aspire run`
// created an empty `yello` catalogue and the Local environment answered every registration with a
// 500 until somebody migrated by hand. Azure runs the migration as a job before the revision is
// promoted; this is the same rule locally - a resource of its own that applies the migrations to the
// database and exits, which the Host waits for. It runs the repository's pinned `dotnet-ef` local tool
// (`dotnet tool restore`, which run.ps1 does first) against the migrations in Yello.Infrastructure, the
// same migrations the bundle carries (the bundle's equivalence test proves the two apply one schema).
var repositoryRoot = Path.GetFullPath(Path.Combine(builder.AppHostDirectory, ".."));

var migrate = builder.AddExecutable("migrate", "dotnet", repositoryRoot)
    .WithArgs(context =>
    {
        context.Args.Add("ef");
        context.Args.Add("database");
        context.Args.Add("update");
        context.Args.Add("--project");
        context.Args.Add("Yello.Infrastructure");
        context.Args.Add("--startup-project");
        context.Args.Add("Yello.Infrastructure");
        context.Args.Add("--connection");
        context.Args.Add(database.Resource.ConnectionStringExpression);
    })
    .WaitFor(database);

builder.AddProject<Projects.Yello_Host>("host", launchProfileName: "https")
    .WithReference(database)
    .WaitFor(database)
    .WaitForCompletion(migrate)
    .WithEnvironment("ClientOrigin", client.GetEndpoint("https"));

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
//     shared value. Pinning a digest is an open question for story 2.6; when it is
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
            "it pins a digest, which Aspire takes separately from a tag. THREE places change " +
            "together, deliberately: Directory.Build.props (the value itself), this file (Aspire " +
            "needs the digest passed apart from the tag), and " +
            "tests/Yello.Tests.Architecture/TestingConventionTests.ExpectedSqlServerImage (which " +
            "asserts the value AC4 names). The fixture needs no change - Testcontainers parses a " +
            "digest reference correctly as it stands");
    }

    var lastColon = remainder.LastIndexOf(':');

    if (lastColon < 0)
    {
        throw InvalidImageReference(reference, "it names no tag");
    }

    // Neither component may be empty. `registry/img:` and `registry/:tag` both split cleanly
    // here while Testcontainers rejects them outright - verified - so accepting them would put
    // the AppHost and the suites back to disagreeing about which engine runs, from one shared
    // value, which is the whole thing this parsing exists to prevent.
    if (lastColon == 0 || lastColon == remainder.Length - 1)
    {
        throw InvalidImageReference(reference, "its image name or its tag is empty");
    }

    return (registry, remainder[..lastColon], remainder[(lastColon + 1)..]);
}

static InvalidOperationException InvalidImageReference(string reference, string why) =>
    new($"'{reference}' cannot be used as a registry/image:tag reference: {why}. " +
        "Directory.Build.props states the image as one string so that the fixture and this file " +
        "cannot disagree about which engine runs.");
