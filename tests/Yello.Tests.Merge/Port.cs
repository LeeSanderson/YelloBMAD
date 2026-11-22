using Xunit;
using Yello.Merge;

namespace Yello.Tests.Merge;

internal static class Port
{
    /// <summary>
    /// Asserts the port admitted the bytes and returns what it read from them.
    /// </summary>
    public static T Admitted<T>(Decoded<T> decoded)
        where T : class
    {
        Assert.Equal(ChangeAdmission.Admitted, decoded.Admission);

        return Assert.IsAssignableFrom<T>(decoded.Value);
    }
}
