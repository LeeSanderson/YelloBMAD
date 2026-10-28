using Xunit;

namespace Yello.Tests.Client;

/// <summary>
/// The collection every browser case belongs to, so one dev server and one browser serve all of
/// them.
/// </summary>
/// <remarks>
/// <para>
/// A collection fixture rather than a class fixture: cases are spread across several classes - story
/// 2.8's <c>KeyboardTests</c> among them, and 2.9's to come - and a class fixture would start a browser
/// per class. It also
/// serialises the suite, which is wanted rather than tolerated - the cases share one dev server
/// process, and per-case isolation comes from a fresh browser context instead.
/// </para>
/// <para>
/// <b>The name is <c>nameof</c> rather than a <c>const</c>.</b> Both attributes need a
/// compile-time constant, so the two ends have to agree on a literal or share one - and a shared
/// <c>public const</c> is what S2339 objects to, correctly, because a consumer would bake the
/// value in at compile time. <c>nameof</c> gives the same single source with none of that: rename
/// the class and both attributes follow.
/// </para>
/// </remarks>
[CollectionDefinition(nameof(ClientSurfaceCollection))]
public sealed class ClientSurfaceCollection : ICollectionFixture<ClientSurfaceFixture>;
