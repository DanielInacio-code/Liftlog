using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using LiftLog.Core.Models;

namespace LiftLog.App.ViewModels;

public partial class RoutineSetItem : ObservableObject
{
    public RoutineSetItem(int setNumber)
    {
        SetNumber = setNumber;
        workingSetNumber = setNumber;
    }

    public RoutineSetItem(RoutineSet routineSet)
    {
        SetNumber = routineSet.SetNumber;
        workingSetNumber = routineSet.SetNumber;
        WeightText = routineSet.Weight == 0
            ? string.Empty
            : routineSet.Weight.ToString("0.###", CultureInfo.CurrentCulture);
        RepetitionsText = routineSet.Repetitions == 0
            ? string.Empty
            : routineSet.Repetitions.ToString(CultureInfo.CurrentCulture);
        SetType = routineSet.IsWarmup
            ? TrainingSetType.Warmup
            : routineSet.SetType;
        Rpe = routineSet.Rpe;
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CompactSetLabel))]
    private int setNumber;

    private int workingSetNumber;

    [ObservableProperty]
    private string weightText = string.Empty;

    [ObservableProperty]
    private string repetitionsText = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CompactSetLabel))]
    [NotifyPropertyChangedFor(nameof(IsWarmup))]
    private TrainingSetType setType;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RpeLabel))]
    private double? rpe;

    public bool IsWarmup => SetType == TrainingSetType.Warmup;

    public string CompactSetLabel => SetType switch
    {
        TrainingSetType.Warmup => "W",
        TrainingSetType.Failure => "F",
        TrainingSetType.Drop => "D",
        _ => workingSetNumber.ToString(CultureInfo.CurrentCulture)
    };

    public string RpeLabel => Rpe?.ToString("0.#", CultureInfo.CurrentCulture) ?? "RPE";

    internal void UpdateWorkingSetNumber(int value)
    {
        if (workingSetNumber == value)
        {
            return;
        }

        workingSetNumber = value;
        OnPropertyChanged(nameof(CompactSetLabel));
    }
}
