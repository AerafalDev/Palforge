namespace Palforge.Commands.Preconditions;

public readonly struct PreconditionResult
{
    public static readonly PreconditionResult Success = new PreconditionResult(true, null);

    public bool IsSuccess { get; }

    public string? ErrorMessage { get; }

    private PreconditionResult(bool isSuccess, string? errorMessage)
    {
        IsSuccess = isSuccess;
        ErrorMessage = errorMessage;
    }

    public static PreconditionResult Fail(string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        return new PreconditionResult(false, message);
    }
}