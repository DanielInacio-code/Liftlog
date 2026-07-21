using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LiftLog.App.Services;
using LiftLog.App.Resources.Strings;
using LiftLog.Core.Services;

namespace LiftLog.App.ViewModels;

public partial class ExerciseDetailsViewModel : BaseViewModel, IQueryAttributable
{
    private readonly IExerciseService _exerciseService;
    private readonly IExerciseImageService _exerciseImageService;
    private readonly INavigationService _navigationService;
    private int _exerciseId;
    private string? _imagePath;

    public ExerciseDetailsViewModel(
        IExerciseService exerciseService,
        IExerciseImageService exerciseImageService,
        INavigationService navigationService)
    {
        _exerciseService = exerciseService;
        _exerciseImageService = exerciseImageService;
        _navigationService = navigationService;
        Title = Resources.Strings.AppText.ExercisesTitle;
    }

    [ObservableProperty]
    private string name = string.Empty;

    [ObservableProperty]
    private string muscleGroup = string.Empty;

    [ObservableProperty]
    private string equipment = string.Empty;

    [ObservableProperty]
    private string instructions = string.Empty;

    [ObservableProperty]
    private string imageSource = ExerciseImageSource.DefaultCustomImage;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TypeLabel))]
    private bool isCustom;

    [ObservableProperty]
    private bool isLoaded;

    public string TypeLabel => IsCustom
        ? Resources.Strings.AppText.CustomExercise
        : Resources.Strings.AppText.PredefinedExercise;

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("ExerciseId", out var value))
        {
            _exerciseId = Convert.ToInt32(value);
        }
    }

    public Task LoadAsync() => RunBusyAsync(async () =>
    {
        var exercise = await _exerciseService.GetByIdAsync(_exerciseId)
            ?? throw new InvalidOperationException("Exercise not found.");

        Name = exercise.Name;
        MuscleGroup = ExerciseDisplayNames.For(exercise.MuscleGroup);
        Equipment = ExerciseDisplayNames.For(exercise.Equipment);
        Instructions = string.IsNullOrWhiteSpace(exercise.Instructions)
            ? Resources.Strings.AppText.NoInstructions
            : exercise.Instructions;
        _imagePath = exercise.ImagePath;
        ImageSource = ExerciseImageSource.For(exercise);
        IsCustom = exercise.IsCustom;
        IsLoaded = true;
    }, AppText.FailedToLoadExercise);

    [RelayCommand]
    private Task RetryAsync() => LoadAsync();

    [RelayCommand]
    private Task EditAsync() =>
        _navigationService.GoToAsync(
            NavigationRoutes.ExerciseEdit,
            new Dictionary<string, object> { ["ExerciseId"] = _exerciseId });

    [RelayCommand]
    private async Task DeleteAsync()
    {
        if (IsBusy)
        {
            return;
        }

        try
        {
            IsBusy = true;
            ErrorMessage = null;
            await _exerciseService.DeleteCustomAsync(_exerciseId);
            _exerciseImageService.DeleteIfOwned(_imagePath);
            await _navigationService.GoBackAsync();
        }
        catch (ExerciseValidationException exception)
        {
            ErrorMessage = exception.Message;
        }
        catch (Exception exception)
        {
            System.Diagnostics.Debug.WriteLine(exception);
            ErrorMessage = AppText.FailedToDeleteExercise;
        }
        finally
        {
            IsBusy = false;
        }
    }
}
