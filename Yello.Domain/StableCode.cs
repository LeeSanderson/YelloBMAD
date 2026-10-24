using System.Globalization;
using System.Text;

namespace Yello.Domain;

/// <summary>
/// The kebab-case code an enum member travels as on the wire - <c>ManageTasks</c> becomes
/// <c>manage-tasks</c>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why derived rather than listed.</b> The client uses a refusal's code directly as a resource
/// key, exactly as it uses a registration failure code, so there is no code-to-key table to fall out
/// of step. Deriving the code from the member name means there is no server-side table either; a
/// gate checks that every code the server can send has client copy behind it.
/// </para>
/// <para>
/// Renaming a member therefore changes a wire code. That is a contract change and is meant to look
/// like one: the client's resource key stops matching and the copy gate fails.
/// </para>
/// </remarks>
public static class StableCode
{
    /// <summary>
    /// The member's name in kebab case.
    /// </summary>
    /// <typeparam name="TEnum">The enum.</typeparam>
    /// <param name="value">The member.</param>
    /// <returns>The code.</returns>
    public static string Of<TEnum>(TEnum value)
        where TEnum : struct, Enum
    {
        var name = value.ToString();
        var code = new StringBuilder(name.Length + 8);

        for (var index = 0; index < name.Length; index++)
        {
            var character = name[index];

            if (char.IsUpper(character) && index > 0)
            {
                code.Append('-');
            }

            // Lower case is the code's form, not a normalisation for comparison: S4040's advice to
            // upper-case applies to strings compared case-insensitively, and nothing compares these.
#pragma warning disable S4040
            code.Append(char.ToLower(character, CultureInfo.InvariantCulture));
#pragma warning restore S4040
        }

        return code.ToString();
    }
}
