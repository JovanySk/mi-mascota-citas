namespace MiMascota.Web.Services;

/// <summary>
/// Outcome of a business operation. Business rule violations (overlapping slot,
/// invalid status change...) are returned as a failure with a user-facing message
/// instead of throwing exceptions.
/// </summary>
public class Result
{
    protected Result(bool isSuccess, string? error)
    {
        IsSuccess = isSuccess;
        Error = error;
    }

    public bool IsSuccess { get; }

    public bool IsFailure => !IsSuccess;

    /// <summary>Message in Spanish, ready to show to the user. Null on success.</summary>
    public string? Error { get; }

    public static Result Success() => new(true, null);

    public static Result Failure(string error) => new(false, error);
}

public class Result<T> : Result
{
    private Result(bool isSuccess, T? value, string? error) : base(isSuccess, error)
    {
        Value = value;
    }

    public T? Value { get; }

    public static Result<T> Success(T value) => new(true, value, null);

    public static new Result<T> Failure(string error) => new(false, default, error);
}
