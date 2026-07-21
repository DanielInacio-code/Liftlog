namespace LiftLog.Core.Services;

public sealed class ExerciseValidationException : Exception
{
    public ExerciseValidationException(string message)
        : base(message)
    {
    }

    public ExerciseValidationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
