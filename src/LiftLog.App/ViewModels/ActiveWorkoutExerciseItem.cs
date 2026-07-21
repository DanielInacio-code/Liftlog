using System.Collections.ObjectModel;
using System.ComponentModel;
using LiftLog.Core.Models;
using LiftLog.Core.Services;
using LiftLog.App.Resources.Strings;
using LiftLog.App.Services;

namespace LiftLog.App.ViewModels;

public sealed class ActiveWorkoutExerciseItem : ObservableCollection<WorkoutSetItem>
{
    private IReadOnlyList<PreviousSetPerformance> _previousSets = [];

    public ActiveWorkoutExerciseItem(WorkoutExercise exercise)
    {
        Id = exercise.Id;
        ExerciseId = exercise.ExerciseId;
        Name = exercise.ExerciseName;
        ImageSource = ExerciseImageSource.ForName(exercise.ExerciseName);
        notes = exercise.Notes ?? string.Empty;
        restTimerSeconds = exercise.RestTimerSeconds;

        foreach (var workoutSet in exercise.Sets.OrderBy(item => item.SetNumber))
        {
            Add(new WorkoutSetItem(workoutSet));
        }
    }

    public int Id { get; }

    public int? ExerciseId { get; }

    public string Name { get; }

    public string ImageSource { get; }

    public string PreviousPerformance { get; set; } = AppText.NoPreviousWorkout;

    private string notes;

    public string Notes
    {
        get => notes;
        set
        {
            if (notes == value)
            {
                return;
            }

            notes = value;
            OnPropertyChanged(new PropertyChangedEventArgs(nameof(Notes)));
        }
    }

    private int restTimerSeconds;

    public int RestTimerSeconds
    {
        get => restTimerSeconds;
        set
        {
            if (restTimerSeconds == value)
            {
                return;
            }

            restTimerSeconds = value;
            OnPropertyChanged(new PropertyChangedEventArgs(nameof(RestTimerSeconds)));
            OnPropertyChanged(new PropertyChangedEventArgs(nameof(RestTimerLabel)));
        }
    }

    public string RestTimerLabel => RestTimerSeconds <= 0
        ? AppText.RestTimerOff
        : $"{AppText.RestTimer}: {FormatRestDuration(RestTimerSeconds)}";

    public ObservableCollection<WorkoutSetItem> Sets => this;

    protected override void InsertItem(int index, WorkoutSetItem item)
    {
        base.InsertItem(index, item);
        item.PropertyChanged += OnSetPropertyChanged;
        RefreshWorkingSetNumbers();
    }

    protected override void RemoveItem(int index)
    {
        var item = this[index];
        base.RemoveItem(index);
        item.PropertyChanged -= OnSetPropertyChanged;
        RefreshWorkingSetNumbers();
    }

    protected override void SetItem(int index, WorkoutSetItem item)
    {
        var previous = this[index];
        base.SetItem(index, item);
        previous.PropertyChanged -= OnSetPropertyChanged;
        item.PropertyChanged += OnSetPropertyChanged;
        RefreshWorkingSetNumbers();
    }

    protected override void MoveItem(int oldIndex, int newIndex)
    {
        base.MoveItem(oldIndex, newIndex);
        RefreshWorkingSetNumbers();
    }

    protected override void ClearItems()
    {
        foreach (var item in this)
        {
            item.PropertyChanged -= OnSetPropertyChanged;
        }

        base.ClearItems();
    }

    public static string FormatRestDuration(int totalSeconds)
    {
        if (totalSeconds <= 0)
        {
            return AppText.Off;
        }

        var minutes = totalSeconds / 60;
        var seconds = totalSeconds % 60;
        if (minutes == 0)
        {
            return $"{seconds}s";
        }

        return seconds == 0
            ? $"{minutes}m"
            : $"{minutes}m {seconds}s";
    }

    public void ApplyPreviousPerformance(PreviousExercisePerformance? previous)
    {
        _previousSets = previous?.AllSets ?? previous?.Sets ?? [];
        RefreshPreviousPerformance();
    }

    public void RefreshPreviousPerformance()
    {
        for (var index = 0; index < Sets.Count; index++)
        {
            Sets[index].PreviousPerformance = index < _previousSets.Count
                ? FormatPreviousSet(_previousSets[index])
                : "—";
        }
    }

    private static string FormatPreviousSet(PreviousSetPerformance previous)
    {
        var weightAndRepetitions =
            $"{WorkoutDisplayFormatter.FormatWeight(previous.Weight)} × {previous.Repetitions}";
        return previous.Rpe is { } rpe
            ? $"{weightAndRepetitions}\nRPE {WorkoutDisplayFormatter.FormatRpe(rpe)}"
            : weightAndRepetitions;
    }

    private void OnSetPropertyChanged(object? sender, PropertyChangedEventArgs eventArgs)
    {
        if (eventArgs.PropertyName == nameof(WorkoutSetItem.SetType))
        {
            RefreshWorkingSetNumbers();
        }
    }

    private void RefreshWorkingSetNumbers()
    {
        var workingSetNumber = 0;
        foreach (var item in this)
        {
            item.UpdateWorkingSetNumber(item.IsWarmup ? 0 : ++workingSetNumber);
        }
    }
}
