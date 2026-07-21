using LiftLog.App.ViewModels;

namespace LiftLog.App.Views;

public partial class ExercisePickerPage : ContentPage, IQueryAttributable
{
    public const string TitleParameter = "PickerTitle";
    public const string AllowedExerciseIdsParameter = "AllowedExerciseIds";
    public const string SelectionCallbackParameter = "SelectionCallback";

    private readonly ExercisePickerViewModel _viewModel;
    private Func<int, Task>? _selectionCallback;
    private bool _isSelecting;

    public ExercisePickerPage(ExercisePickerViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        var title = query.TryGetValue(TitleParameter, out var titleValue)
            ? titleValue?.ToString() ?? "Select exercise"
            : "Select exercise";
        var allowedExerciseIds = query.TryGetValue(AllowedExerciseIdsParameter, out var idsValue) &&
                                 idsValue is IEnumerable<int> ids
            ? ids
            : [];

        _selectionCallback = query.TryGetValue(SelectionCallbackParameter, out var callbackValue)
            ? callbackValue as Func<int, Task>
            : null;
        _viewModel.Configure(title, allowedExerciseIds);
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.LoadAsync();
    }

    private async void OnExerciseSelected(object? sender, SelectionChangedEventArgs eventArgs)
    {
        if (_isSelecting ||
            _selectionCallback is null ||
            eventArgs.CurrentSelection.FirstOrDefault() is not ExerciseListItem exercise)
        {
            return;
        }

        ExercisesCollection.SelectedItem = null;
        _isSelecting = true;
        try
        {
            await _selectionCallback(exercise.Id);
            await Shell.Current.GoToAsync("..");
        }
        finally
        {
            _isSelecting = false;
        }
    }
}
