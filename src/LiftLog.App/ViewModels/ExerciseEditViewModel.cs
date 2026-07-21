using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LiftLog.App.Services;
using LiftLog.App.Resources.Strings;
using LiftLog.Core.Models;
using LiftLog.Core.Services;

namespace LiftLog.App.ViewModels;

public partial class ExerciseEditViewModel : BaseViewModel, IQueryAttributable
{
    private readonly IExerciseService _exerciseService;
    private readonly IExerciseImageService _exerciseImageService;
    private readonly INavigationService _navigationService;
    private int? _exerciseId;
    private string? _originalImagePath;
    private string? _pendingImagePath;
    private bool _hasLoaded;

    public ExerciseEditViewModel(
        IExerciseService exerciseService,
        IExerciseImageService exerciseImageService,
        INavigationService navigationService)
    {
        _exerciseService = exerciseService;
        _exerciseImageService = exerciseImageService;
        _navigationService = navigationService;

        MuscleGroups = Enum.GetValues<MuscleGroup>()
            .Select(group => new MuscleGroupOption(ExerciseDisplayNames.For(group), group))
            .OrderBy(option => option.DisplayName)
            .ToList();

        EquipmentOptions = Enum.GetValues<Equipment>()
            .Select(equipment => new EquipmentOption(ExerciseDisplayNames.For(equipment), equipment))
            .OrderBy(option => option.DisplayName)
            .ToList();

        selectedEquipment = EquipmentOptions.Single(option => option.Value == Equipment.None);
        PageTitle = Resources.Strings.AppText.CreateExerciseTitle;
    }

    public IReadOnlyList<MuscleGroupOption> MuscleGroups { get; }

    public IReadOnlyList<EquipmentOption> EquipmentOptions { get; }

    [ObservableProperty]
    private string pageTitle;

    [ObservableProperty]
    private string name = string.Empty;

    [ObservableProperty]
    private string instructions = string.Empty;

    [ObservableProperty]
    private MuscleGroupOption? selectedMuscleGroup;

    [ObservableProperty]
    private EquipmentOption selectedEquipment;

    [ObservableProperty]
    private bool isEditMode;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasCustomImage))]
    private string? imagePath;

    [ObservableProperty]
    private string imageSource = ExerciseImageSource.DefaultCustomImage;

    public bool HasCustomImage => !string.IsNullOrWhiteSpace(ImagePath);

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("ExerciseId", out var value))
        {
            _exerciseId = Convert.ToInt32(value);
            IsEditMode = true;
            PageTitle = Resources.Strings.AppText.EditExerciseTitle;
        }
    }

    public Task LoadAsync()
    {
        if (_hasLoaded)
        {
            return Task.CompletedTask;
        }

        if (_exerciseId is null)
        {
            _hasLoaded = true;
            return Task.CompletedTask;
        }

        return RunBusyAsync(async () =>
        {
            var exercise = await _exerciseService.GetByIdAsync(_exerciseId.Value)
                ?? throw new InvalidOperationException("Exercise not found.");

            if (!exercise.IsCustom)
            {
                throw new ExerciseValidationException("Built-in exercises cannot be edited.");
            }

            Name = exercise.Name;
            Instructions = exercise.Instructions ?? string.Empty;
            SelectedMuscleGroup = MuscleGroups.Single(option => option.Value == exercise.MuscleGroup);
            SelectedEquipment = EquipmentOptions.Single(option => option.Value == exercise.Equipment);
            _originalImagePath = exercise.ImagePath;
            ImagePath = exercise.ImagePath;
            ImageSource = ExerciseImageSource.For(exercise);
            _hasLoaded = true;
        }, AppText.FailedToLoadExercise);
    }

    [RelayCommand]
    private async Task ChooseImageAsync()
    {
        try
        {
            ErrorMessage = null;
            var selectedPath = await _exerciseImageService.PickAndSaveAsync();
            if (selectedPath is null)
            {
                return;
            }

            _exerciseImageService.DeleteIfOwned(_pendingImagePath);
            _pendingImagePath = selectedPath;
            ImagePath = selectedPath;
            ImageSource = selectedPath;
        }
        catch (Exception exception)
        {
            System.Diagnostics.Debug.WriteLine(exception);
            ErrorMessage = AppText.FailedToChooseExercisePhoto;
        }
    }

    [RelayCommand]
    private void RemoveImage()
    {
        _exerciseImageService.DeleteIfOwned(_pendingImagePath);
        _pendingImagePath = null;
        ImagePath = null;
        ImageSource = ExerciseImageSource.DefaultCustomImage;
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (IsBusy)
        {
            return;
        }

        if (SelectedMuscleGroup?.Value is null)
        {
            ErrorMessage = AppText.SelectMuscleGroupValidation;
            return;
        }

        try
        {
            IsBusy = true;
            ErrorMessage = null;

            var input = new ExerciseInput(
                Name,
                SelectedMuscleGroup.Value.Value,
                SelectedEquipment.Value,
                Instructions,
                ImagePath);

            if (_exerciseId is null)
            {
                await _exerciseService.CreateCustomAsync(input);
            }
            else
            {
                await _exerciseService.UpdateCustomAsync(_exerciseId.Value, input);
            }

            if (!string.Equals(_originalImagePath, ImagePath, StringComparison.OrdinalIgnoreCase))
            {
                _exerciseImageService.DeleteIfOwned(_originalImagePath);
            }

            _pendingImagePath = null;

            await _navigationService.GoBackAsync();
        }
        catch (ExerciseValidationException exception)
        {
            ErrorMessage = exception.Message;
        }
        catch (Exception exception)
        {
            System.Diagnostics.Debug.WriteLine(exception);
            ErrorMessage = AppText.FailedToSaveExercise;
        }
        finally
        {
            IsBusy = false;
        }
    }
}
