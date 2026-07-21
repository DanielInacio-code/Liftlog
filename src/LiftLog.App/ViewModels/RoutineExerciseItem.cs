using System.Collections.ObjectModel;
using System.ComponentModel;
using LiftLog.App.Services;
using LiftLog.Core.Models;

namespace LiftLog.App.ViewModels;

public sealed class RoutineExerciseItem : ObservableCollection<RoutineSetItem>
{
    public RoutineExerciseItem(Exercise exercise)
    {
        Id = exercise.Id;
        Name = exercise.Name;
        Details =
            $"{ExerciseDisplayNames.For(exercise.MuscleGroup)} · {ExerciseDisplayNames.For(exercise.Equipment)}";
        ImageSource = ExerciseImageSource.ForThumbnail(exercise);
    }

    public int Id { get; }

    public string Name { get; }

    public string Details { get; }

    public string ImageSource { get; }

    public ObservableCollection<RoutineSetItem> Sets => this;

    protected override void InsertItem(int index, RoutineSetItem item)
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

    protected override void SetItem(int index, RoutineSetItem item)
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

    private void OnSetPropertyChanged(object? sender, PropertyChangedEventArgs eventArgs)
    {
        if (eventArgs.PropertyName == nameof(RoutineSetItem.SetType))
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
