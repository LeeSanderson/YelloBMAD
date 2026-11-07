using Xunit;
using Yello.Tests.Client;
using Yello.Tests.Shared;

// One SQL Server for this test process, started on first use - only RealHostCollection asks for one,
// so the FakeYelloApi cases never wait for it. Story 1.9 Q1 (a); ContainerTopologyGateTests holds the
// shape.
[assembly: AssemblyFixture(typeof(SqlServerEngine))]

// One client server and one Chromium for the whole process. Each class is its own collection, so
// classes run in parallel, each case in a fresh browser context. Cases within a class stay serial.
// Four at once, measured: about 4 minutes against 10.5 serial. Six ran the machine out of memory, since every
// context holds its own WebAssembly runtime.
[assembly: AssemblyFixture(typeof(ClientSurfaceFixture))]
[assembly: Xunit.v3.Parallelization(MaxThreads = 4)]
