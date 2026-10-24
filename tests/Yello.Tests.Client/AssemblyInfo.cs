using Xunit;
using Yello.Tests.Shared;

// One SQL Server for this test process, started on first use - only RealHostCollection asks for one,
// so the FakeYelloApi cases never wait for it. Story 1.9 Q1 (a); ContainerTopologyGateTests holds the
// shape.
[assembly: AssemblyFixture(typeof(SqlServerEngine))]
