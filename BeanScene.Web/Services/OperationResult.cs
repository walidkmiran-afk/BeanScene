namespace BeanScene.Web.Services;

public class OperationResult
{
    public bool Succeeded { get; protected set; }
    public List<string> Errors { get; } = new();

    public static OperationResult Ok() => new() { Succeeded = true };

    public static OperationResult Fail(params string[] errors)
    {
        var result = new OperationResult { Succeeded = false };
        result.Errors.AddRange(errors);
        return result;
    }

    public static OperationResult Fail(IEnumerable<string> errors) => Fail(errors.ToArray());
}

public class OperationResult<T> : OperationResult
{
    public T? Value { get; private set; }

    public static OperationResult<T> Ok(T value) => new() { Succeeded = true, Value = value };

    public static new OperationResult<T> Fail(params string[] errors)
    {
        var result = new OperationResult<T> { Succeeded = false };
        result.Errors.AddRange(errors);
        return result;
    }
}
