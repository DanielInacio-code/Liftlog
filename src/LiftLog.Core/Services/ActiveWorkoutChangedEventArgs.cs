using LiftLog.Core.Models;

namespace LiftLog.Core.Services;

public sealed class ActiveWorkoutChangedEventArgs(WorkoutSession? workout) : EventArgs
{
    public WorkoutSession? Workout { get; } = workout;
}
