using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LiftLog.App.Controls;
using LiftLog.App.Services;
using LiftLog.App.Resources.Strings;
using LiftLog.Core.Services;

namespace LiftLog.App.ViewModels;

public partial class ProgressViewModel(
    IExerciseService exerciseService,
    IStatisticsService statisticsService) : BaseViewModel
{
    private IReadOnlyList<ExerciseProgressPoint> _progressPoints = [];
    private ExerciseChartMetric _chartMetric = ExerciseChartMetric.Weight;

    public ObservableCollection<ProgressExerciseOption> ExerciseOptions { get; } = [];

    private ObservableCollection<ExerciseProgressPointItem> _evolution = [];

    public ObservableCollection<ExerciseProgressPointItem> Evolution
    {
        get => _evolution;
        private set => SetProperty(ref _evolution, value);
    }

    [ObservableProperty]
    private ProgressExerciseOption? selectedExercise;

    [ObservableProperty]
    private string bestWeight = "0 kg";

    [ObservableProperty]
    private string bestRepetitions = "0";

    [ObservableProperty]
    private string totalVolume = "0 kg";

    [ObservableProperty]
    private string workoutCount = "0";

    [ObservableProperty]
    private bool hasProgress;

    [ObservableProperty]
    private bool isProgressEmpty = true;

    [ObservableProperty]
    private IReadOnlyList<ChartDataPoint> chartPoints = [];

    [ObservableProperty]
    private bool isWeightSelected = true;

    [ObservableProperty]
    private bool isVolumeSelected;

    [ObservableProperty]
    private bool isRepsSelected;

    public async Task LoadAsync()
    {
        if (ExerciseOptions.Count == 0)
        {
            await RunBusyAsync(async () =>
            {
                var exercises = await exerciseService.GetAllAsync();
                foreach (var exercise in exercises)
                {
                    ExerciseOptions.Add(new ProgressExerciseOption(exercise));
                }

                SelectedExercise = ExerciseOptions.FirstOrDefault();
            }, AppText.FailedToLoadExercises);
        }

        await LoadSelectedAsync();
    }

    public Task LoadSelectedAsync()
    {
        if (SelectedExercise is null)
        {
            _progressPoints = [];
            ChartPoints = [];
            Evolution = [];
            HasProgress = false;
            IsProgressEmpty = true;
            return Task.CompletedTask;
        }

        return RunBusyAsync(async () =>
        {
            var progress = await statisticsService.GetExerciseProgressAsync(SelectedExercise.Id);

            BestWeight = WorkoutDisplayFormatter.FormatWeight(progress.BestWeight);
            BestRepetitions = progress.BestRepetitions.ToString();
            TotalVolume = WorkoutDisplayFormatter.FormatVolume(progress.TotalVolume);
            WorkoutCount = progress.WorkoutCount.ToString();

            _progressPoints = progress.Points;
            RefreshChart();

            Evolution = new ObservableCollection<ExerciseProgressPointItem>(
                progress.Points.Select(point => new ExerciseProgressPointItem(point)));

            HasProgress = Evolution.Count > 0;
            IsProgressEmpty = !HasProgress;
        }, AppText.FailedToLoadProgress);
    }

    [RelayCommand]
    private Task RetryAsync() => ExerciseOptions.Count == 0 ? LoadAsync() : LoadSelectedAsync();

    [RelayCommand]
    private void SelectChartMetric(string metricName)
    {
        if (!Enum.TryParse<ExerciseChartMetric>(metricName, out var metric))
        {
            return;
        }

        _chartMetric = metric;
        IsWeightSelected = metric == ExerciseChartMetric.Weight;
        IsVolumeSelected = metric == ExerciseChartMetric.Volume;
        IsRepsSelected = metric == ExerciseChartMetric.Reps;
        RefreshChart();
    }

    private void RefreshChart()
    {
        ChartPoints = _progressPoints
            .Select(point => new ChartDataPoint(
                point.StartedAt.ToLocalTime().ToString("dd MMM"),
                _chartMetric switch
                {
                    ExerciseChartMetric.Weight => (double)point.MaximumWeight,
                    ExerciseChartMetric.Volume => (double)point.Volume,
                    _ => point.BestRepetitions
                }))
            .ToList();
    }
}

public enum ExerciseChartMetric
{
    Weight,
    Volume,
    Reps
}
