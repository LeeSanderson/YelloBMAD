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
// The tag itself still floats by cumulative update - `2025-latest` is not a digest. That is
// the one unpinned input in the whole stack and is raised as an open question for Lee rather
// than silently accepted.
var sqlServer = builder.AddSqlServer("sql")
    .WithImageRegistry("mcr.microsoft.com")
    .WithImage("mssql/server")
    .WithImageTag("2025-latest");

var database = sqlServer.AddDatabase("yello");

// WaitFor is orchestration-level sequencing performed by the AppHost, not a probe inside
// Yello.Host. AR-33's ban is on the application touching the database to answer a liveness
// or readiness probe, and on any component doing so on an unconditional timer; neither
// applies to the AppHost holding a process back until its dependency is accepting
// connections.
builder.AddProject<Projects.Yello_Host>("host")
    .WithReference(database)
    .WaitFor(database);

builder.AddProject<Projects.Yello_Client>("client");

// RunAsync rather than Run (S6966): the blocking overload parks the entry-point thread for
// the whole orchestration session. Awaiting it turns this file into an async entry point,
// which costs nothing here and is the shape Yello.Host already uses.
await builder.Build().RunAsync();
