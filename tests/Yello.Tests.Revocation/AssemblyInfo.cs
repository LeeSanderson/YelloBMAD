using Xunit;
using Yello.Tests.Shared;

// One SQL Server for this test process, started on first use; every test class takes a database of
// its own on it. Story 1.9 Q1 (a); ContainerTopologyGateTests holds the shape.
[assembly: AssemblyFixture(typeof(SqlServerEngine))]
