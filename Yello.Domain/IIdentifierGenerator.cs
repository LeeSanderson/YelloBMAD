namespace Yello.Domain;

/// <summary>
/// Produces the identity of a new row, application-side and before the insert.
/// </summary>
/// <remarks>
/// <para>
/// <b>Application-side generation is what resolves AD-2's registration seam.</b> AD-2 requires
/// <c>sp_set_session_context 'SpaceId', ..., @read_only = 1</c> at the start of every unit of
/// work, "from <c>ActiveSpaceContext</c> and never from a client-supplied value". Registration is
/// unauthenticated, has no <c>ActiveSpaceContext</c>, and creates the very Space whose id the
/// context would need. Because this generator runs before the insert, the slice can produce the
/// <c>SpaceId</c>, set the session context to it inside the transaction, and then write - a
/// value that is server-generated rather than client-supplied, so the prohibition is honoured
/// rather than bypassed. AD-24 names bypassing it, "and that bypass then spreading", as the
/// failure it exists to prevent.
/// </para>
/// <para>
/// <b>Sequential, deliberately, and neither of the two obvious alternatives.</b> AR-34 and
/// <c>ARCHITECTURE-SPINE.md:268</c> fix this: not a database identity column (which would put
/// generation after the insert, defeating the paragraph above), not <c>Guid.NewGuid()</c> (whose
/// random values fragment a clustered index), and explicitly <b>not</b>
/// <c>Guid.CreateVersion7()</c>. The implementation is EF Core's
/// <c>SequentialGuidValueGenerator</c>, whose byte ordering is the one SQL Server's
/// <c>uniqueidentifier</c> comparison semantics actually sort by - which is the property that
/// makes it sequential <i>in the index</i> rather than merely sequential in memory.
/// </para>
/// <para>
/// It is a port rather than a direct call because the implementation is an EF Core type and this
/// is <c>Yello.Domain</c>, which references nothing. Gate B scans bytecode and fails the build on
/// an EF Core type reached from this ring.
/// </para>
/// </remarks>
public interface IIdentifierGenerator
{
    /// <summary>
    /// The next identity.
    /// </summary>
    Guid Next();
}
