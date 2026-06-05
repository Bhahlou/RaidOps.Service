namespace RaidOps.Application.Contracts.Common;

public class Result<TSuccess>
{
    private readonly TSuccess? _successValue;
    private readonly string? _errorValue;
    private readonly string? _detailValue;

    public bool IsFailed => _errorValue != null;
    public bool IsSuccess => !IsFailed;
    public TSuccess? Value => _successValue;
    public string? Error => _errorValue;
    public string? Detail => _detailValue;

    protected Result(string errorValue, string? detailValue = null)
    {
        _errorValue = errorValue;
        _detailValue = detailValue;
    }

    protected Result(TSuccess successValue)
    {
        _successValue = successValue;
    }

    public static Result<TSuccess> Ok(TSuccess success)
    {
        ArgumentNullException.ThrowIfNull(success);
        return new Result<TSuccess>(success);
    }

    public static Result<TSuccess> Fail(string error, string? detail = null) =>
        new(error, detail);
}