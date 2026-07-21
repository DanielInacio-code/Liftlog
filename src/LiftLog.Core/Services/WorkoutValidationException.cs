namespace LiftLog.Core.Services;

public sealed class WorkoutValidationException : Exception
{
    public WorkoutValidationException(string message) : base(message)
    {
    }

    public WorkoutValidationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
