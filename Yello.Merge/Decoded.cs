namespace Yello.Merge;

/// <summary>
/// What the port made of bytes it was given: the value when <see cref="Admission"/> is
/// <see cref="ChangeAdmission.Admitted"/>, otherwise <c>null</c> and the reason it was refused.
/// </summary>
public readonly record struct Decoded<T>(ChangeAdmission Admission, T? Value)
    where T : class;
