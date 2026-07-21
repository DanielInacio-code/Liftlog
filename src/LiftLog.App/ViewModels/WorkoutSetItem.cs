using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using LiftLog.Core.Models;
using LiftLog.Core.Services;
using LiftLog.App.Resources.Strings;

namespace LiftLog.App.ViewModels;

public partial class WorkoutSetItem : ObservableObject
{
    private WorkoutSetInput _savedInput;

    public WorkoutSetItem(WorkoutSet workoutSet)
    {
        Id = workoutSet.Id;
        setNumber = workoutSet.SetNumber;
        workingSetNumber = workoutSet.SetNumber;
        weightText = workoutSet.Weight == 0
            ? string.Empty
            : workoutSet.Weight.ToString("0.###", CultureInfo.CurrentCulture);
        repetitionsText = workoutSet.Repetitions == 0
            ? string.Empty
            : workoutSet.Repetitions.ToString(CultureInfo.CurrentCulture);
        setType = workoutSet.IsWarmup
            ? TrainingSetType.Warmup
            : workoutSet.SetType;
        rpe = workoutSet.Rpe;
        isCompleted = workoutSet.IsCompleted;
        _savedInput = new WorkoutSetInput(
            workoutSet.Weight,
            workoutSet.Repetitions,
            setType == TrainingSetType.Warmup,
            workoutSet.Rpe,
            setType);
    }

    public int Id { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CompactSetLabel))]
    private int setNumber;

    private int workingSetNumber;

    [ObservableProperty]
    private string weightText;

    [ObservableProperty]
    private string repetitionsText;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CompactSetLabel))]
    [NotifyPropertyChangedFor(nameof(IsWarmup))]
    private TrainingSetType setType;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RpeLabel))]
    private double? rpe;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CompletionButtonText))]
    [NotifyPropertyChangedFor(nameof(CompletionGlyph))]
    [NotifyPropertyChangedFor(nameof(StatusText))]
    private bool isCompleted;

    [ObservableProperty]
    private bool isPersonalRecord;

    [ObservableProperty]
    private string previousPerformance = "—";

    public string SetLabel => AppText.SetLabel(SetNumber);

    public string CompletionButtonText => IsCompleted ? AppText.ReopenSet : AppText.CompleteSet;

    public string CompletionGlyph => IsCompleted ? "✓" : "○";

    public bool IsWarmup => SetType == TrainingSetType.Warmup;

    public string CompactSetLabel => SetType switch
    {
        TrainingSetType.Warmup => "W",
        TrainingSetType.Failure => "F",
        TrainingSetType.Drop => "D",
        _ => workingSetNumber.ToString(CultureInfo.CurrentCulture)
    };

    public string RpeLabel => Rpe?.ToString("0.#", CultureInfo.CurrentCulture) ?? "RPE";

    public string StatusText => IsCompleted ? AppText.CompletedAndSaved : AppText.Incomplete;

    public bool MatchesSavedInput(WorkoutSetInput input) => _savedInput == input;

    public void MarkSaved(WorkoutSetInput input) => _savedInput = input;

    internal void UpdateWorkingSetNumber(int value)
    {
        if (workingSetNumber == value)
        {
            return;
        }

        workingSetNumber = value;
        OnPropertyChanged(nameof(CompactSetLabel));
    }

    partial void OnSetNumberChanged(int value) => OnPropertyChanged(nameof(SetLabel));
}
