namespace LiftLog.Core.Services;

public sealed class RoutineValidationException : Exception
{
    public RoutineValidationException(string message) : base(message)
    {
    }

    public RoutineValidationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
