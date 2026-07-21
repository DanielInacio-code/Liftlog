namespace LiftLog.App.Services;

public sealed class WorkoutHubState
{
    public WorkoutHubSection CurrentSection { get; set; } = WorkoutHubSection.Routines;
}

public enum WorkoutHubSection
{
    Routines,
    Exercises,
    History
}
