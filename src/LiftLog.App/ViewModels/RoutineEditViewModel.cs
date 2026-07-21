using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LiftLog.App.Services;
using LiftLog.App.Resources.Strings;
using LiftLog.Core.Services;

namespace LiftLog.App.ViewModels;

public partial class RoutineEditViewModel : BaseViewModel, IQueryAttributable
{
    private readonly IExerciseService _exerciseService;
    private readonly IRoutineService _routineService;
    private readonly INavigationService _navigationService;
    private readonly List<RoutineExerciseItem> _allExercises = [];
    private int? _routineId;
    private bool _loaded;

    public RoutineEditViewModel(
        IExerciseService exerciseService,
        IRoutineService routineService,
        INavigationService navigationService)
    {
        _exerciseService = exerciseService;
        _routineService = routineService;
        _navigationService = navigationService;
        PageTitle = Resources.Strings.AppText.CreateRoutineTitle;
    }

    public ObservableCollection<RoutineExerciseItem> SelectedExercises { get; } = [];

    public ObservableCollection<RoutineExerciseItem> AvailableExercises { get; } = [];

    [ObservableProperty]
    private string pageTitle;

    [ObservableProperty]
    private string name = string.Empty;

    [ObservableProperty]
    private RoutineExerciseItem? exerciseToAdd;

    [ObservableProperty]
    private bool isEditMode;

    [ObservableProperty]
    private bool hasSelectedExercises;

    [ObservableProperty]
    private bool isSelectionEmpty = true;

    [ObservableProperty]
    private bool hasAvailableExercises;

    [ObservableProperty]
    private bool isExerciseCatalogEmpty;

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("RoutineId", out var value))
        {
            _routineId = Convert.ToInt32(value);
            IsEditMode = true;
            PageTitle = Resources.Strings.AppText.EditRoutineTitle;
        }
    }

    public Task LoadAsync()
    {
        if (_loaded)
        {
            return Task.CompletedTask;
        }

        return RunBusyAsync(async () =>
        {
            var exercises = await _exerciseService.GetAllAsync();
            _allExercises.Clear();
            _allExercises.AddRange(exercises.Select(exercise => new RoutineExerciseItem(exercise)));

            if (_routineId is not null)
            {
                var routine = await _routineService.GetByIdAsync(_routineId.Value)
                    ?? throw new RoutineValidationException("The routine was not found.");

                Name = routine.Name;
                var byId = _allExercises.ToDictionary(item => item.Id);
                foreach (var routineExercise in routine.Exercises.OrderBy(item => item.Position))
                {
                    if (byId.TryGetValue(routineExercise.ExerciseId, out var item))
                    {
                        foreach (var routineSet in routineExercise.Sets.OrderBy(set => set.SetNumber))
                        {
                            item.Sets.Add(new RoutineSetItem(routineSet));
                        }

                        AddDefaultSetsIfEmpty(item);
                        SelectedExercises.Add(item);
                    }
                }
            }

            RefreshCollections();
            _loaded = true;
        }, AppText.FailedToLoadRoutineEditor);
    }

    [RelayCommand]
    private void AddExercise()
    {
        if (ExerciseToAdd is null)
        {
            ErrorMessage = AppText.SelectExerciseToAdd;
            return;
        }

        AddDefaultSetsIfEmpty(ExerciseToAdd);
        SelectedExercises.Add(ExerciseToAdd);
        ExerciseToAdd = null;
        ErrorMessage = null;
        RefreshCollections();
    }

    [RelayCommand]
    private void RemoveExercise(RoutineExerciseItem exercise)
    {
        SelectedExercises.Remove(exercise);
        RefreshCollections();
    }

    [RelayCommand]
    private void MoveExerciseUp(RoutineExerciseItem exercise)
    {
        var index = SelectedExercises.IndexOf(exercise);
        if (index > 0)
        {
            SelectedExercises.Move(index, index - 1);
        }
    }

    [RelayCommand]
    private void MoveExerciseDown(RoutineExerciseItem exercise)
    {
        var index = SelectedExercises.IndexOf(exercise);
        if (index >= 0 && index < SelectedExercises.Count - 1)
        {
            SelectedExercises.Move(index, index + 1);
        }
    }

    [RelayCommand]
    private void AddSet(RoutineExerciseItem exercise)
    {
        if (exercise.Sets.Count >= 20)
        {
            ErrorMessage = "An exercise can contain at most 20 sets.";
            return;
        }

        exercise.Sets.Add(new RoutineSetItem(exercise.Sets.Count + 1));
        ErrorMessage = null;
    }

    [RelayCommand]
    private void DeleteSet(RoutineSetItem routineSet)
    {
        var exercise = SelectedExercises.FirstOrDefault(item => item.Sets.Contains(routineSet));
        if (exercise is null)
        {
            return;
        }

        exercise.Sets.Remove(routineSet);
        RenumberSets(exercise);
    }

    public void ReplaceExercise(
        RoutineExerciseItem current,
        RoutineExerciseItem replacement)
    {
        var index = SelectedExercises.IndexOf(current);
        if (index < 0 || !AvailableExercises.Contains(replacement))
        {
            return;
        }

        replacement.Sets.Clear();
        foreach (var routineSet in current.Sets)
        {
            replacement.Sets.Add(new RoutineSetItem(routineSet.SetNumber)
            {
                WeightText = routineSet.WeightText,
                RepetitionsText = routineSet.RepetitionsText,
                SetType = routineSet.SetType,
                Rpe = routineSet.Rpe
            });
        }

        SelectedExercises[index] = replacement;
        RefreshCollections();
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (IsBusy)
        {
            return;
        }

        try
        {
            IsBusy = true;
            ErrorMessage = null;

            var plans = SelectedExercises
                .Select(exercise => new RoutineExerciseInput(
                    exercise.Id,
                    exercise.Sets.Select(ParseSetInput).ToArray()))
                .ToArray();
            var input = new RoutineInput(
                Name,
                plans.Select(item => item.ExerciseId).ToArray(),
                plans);
            if (_routineId is null)
            {
                await _routineService.CreateAsync(input);
            }
            else
            {
                await _routineService.UpdateAsync(_routineId.Value, input);
            }

            await _navigationService.GoBackAsync();
        }
        catch (RoutineValidationException exception)
        {
            ErrorMessage = exception.Message;
        }
        catch (Exception exception)
        {
            System.Diagnostics.Debug.WriteLine(exception);
            ErrorMessage = AppText.FailedToSaveRoutine;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void RefreshCollections()
    {
        var selectedIds = SelectedExercises.Select(item => item.Id).ToHashSet();
        AvailableExercises.Clear();

        foreach (var exercise in _allExercises.Where(item => !selectedIds.Contains(item.Id)))
        {
            AvailableExercises.Add(exercise);
        }

        HasSelectedExercises = SelectedExercises.Count > 0;
        IsSelectionEmpty = !HasSelectedExercises;
        HasAvailableExercises = AvailableExercises.Count > 0;
        IsExerciseCatalogEmpty = _allExercises.Count == 0;
    }

    private static RoutineSetInput ParseSetInput(RoutineSetItem routineSet)
    {
        if (!TryParseDecimal(routineSet.WeightText, out var weight))
        {
            throw new RoutineValidationException(AppText.InvalidWeight);
        }

        var repetitionsText = routineSet.RepetitionsText.Trim();
        var repetitions = 0;
        if (!string.IsNullOrWhiteSpace(repetitionsText) &&
            !int.TryParse(
                repetitionsText,
                NumberStyles.Integer,
                CultureInfo.CurrentCulture,
                out repetitions))
        {
            throw new RoutineValidationException(AppText.InvalidRepetitions);
        }

        return new RoutineSetInput(
            weight,
            repetitions,
            routineSet.IsWarmup,
            routineSet.Rpe,
            routineSet.SetType);
    }

    private static bool TryParseDecimal(string text, out decimal value)
    {
        text = text.Trim();
        if (string.IsNullOrWhiteSpace(text))
        {
            value = 0;
            return true;
        }

        const NumberStyles styles =
            NumberStyles.AllowLeadingSign |
            NumberStyles.AllowDecimalPoint;

        return decimal.TryParse(text, styles, CultureInfo.CurrentCulture, out value) ||
               decimal.TryParse(text, styles, CultureInfo.InvariantCulture, out value) ||
               decimal.TryParse(
                   text.Replace(',', '.'),
                   styles,
                   CultureInfo.InvariantCulture,
                   out value);
    }

    private static void AddDefaultSetsIfEmpty(RoutineExerciseItem exercise)
    {
        if (exercise.Sets.Count > 0)
        {
            return;
        }

        exercise.Sets.Add(new RoutineSetItem(1));
    }

    private static void RenumberSets(RoutineExerciseItem exercise)
    {
        for (var index = 0; index < exercise.Sets.Count; index++)
        {
            exercise.Sets[index].SetNumber = index + 1;
        }
    }
}
