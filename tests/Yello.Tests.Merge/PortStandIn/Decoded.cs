// Declared in the production namespace on purpose, so moving the file into Yello.Merge changes nothing that uses it.
#pragma warning disable IDE0130
namespace Yello.Merge;
#pragma warning restore IDE0130

/// <summary>
/// What the port made of bytes it was given: the value when <see cref="Admission"/> is
/// <see cref="ChangeAdmission.Admitted"/>, otherwise <c>null</c> and the reason it was refused.
/// </summary>
/// <remarks>
/// A stand-in declared in the test project; see <see cref="ITextMergeStrategy"/>'s remarks.
/// </remarks>
public readonly record struct Decoded<T>(ChangeAdmission Admission, T? Value)
    where T : class;
