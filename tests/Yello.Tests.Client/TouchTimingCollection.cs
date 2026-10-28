using Xunit;

namespace Yello.Tests.Client;

/// <summary>
/// The collection for the touch drag cases: they measure time - a long-press threshold, a window inside which a lift must
/// not happen - so they run alone, with nothing else in the process competing for the browser (CLAUDE.md: timing tests
/// belong in a <c>DisableParallelization</c> collection). It takes the same fixture as <see cref="ClientSurfaceCollection"/>
/// - no new fixture class - and, running after every parallel collection has finished, never holds a second dev server
/// beside the first.
/// </summary>
[CollectionDefinition(nameof(TouchTimingCollection), DisableParallelization = true)]
public sealed class TouchTimingCollection : ICollectionFixture<ClientSurfaceFixture>;
