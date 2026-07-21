using System.Collections.ObjectModel;
using LiftLog.App.Services;
using LiftLog.Core.Models;
using LiftLog.Core.Services;

namespace LiftLog.App.ViewModels;

public sealed class WorkoutDetailsExerciseItem : ObservableCollection<WorkoutDetailsSetItem>
{
    public WorkoutDetailsExerciseItem(
        WorkoutExercise exercise,
        IHistoryService historyService,
        IReadOnlySet<int> personalRecordSetIds)
        : base(exercise.Sets
            .OrderBy(item => item.SetNumber)
            .Select(item => new WorkoutDetailsSetItem(item, personalRecordSetIds.Contains(item.Id))))
    {
        Name = exercise.ExerciseName;
        Notes = exercise.Notes;
        Volume = $"Volume: {WorkoutDisplayFormatter.FormatVolume(historyService.CalculateVolume(exercise))}";
    }

    public string Name { get; }

    public string? Notes { get; }

    public bool HasNotes => !string.IsNullOrWhiteSpace(Notes);

    public string Volume { get; }

    public ObservableCollection<WorkoutDetailsSetItem> Sets => this;
}
