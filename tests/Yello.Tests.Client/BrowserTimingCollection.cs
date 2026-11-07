using Xunit;

namespace Yello.Tests.Client;

/// <summary>
/// The browser cases that must run with nothing else in the process competing for the CPU.
/// </summary>
/// <remarks>
/// Every other browser class runs in parallel with its neighbours. These measure time on the page's clock (a long-press
/// threshold, a window in which a lift must not happen, a render budget) or what the page holds after a switch, and a
/// neighbour's boot would skew either. xunit runs a <c>DisableParallelization</c> collection after the parallel ones, on
/// the same <see cref="ClientSurfaceFixture"/>.
/// </remarks>
[CollectionDefinition(nameof(BrowserTimingCollection), DisableParallelization = true)]
public sealed class BrowserTimingCollection;
