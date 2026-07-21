using System.Collections.ObjectModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LiftLog.App.Services;
using LiftLog.App.Resources.Strings;
using LiftLog.Core.Services;

namespace LiftLog.App.ViewModels;

public partial class HistoryViewModel(
    IHistoryService historyService,
    INavigationService navigationService) : BaseViewModel
{
    private const int PageSize = 30;
    private bool _hasMore;

    [ObservableProperty]
    private ObservableCollection<HistoryWorkoutListItem> workouts = [];

    [ObservableProperty]
    private bool isEmpty;

    [ObservableProperty]
    private bool isLoadingMore;

    public Task LoadAsync() => RunBusyAsync(async () =>
    {
        var sessions = await Task.Run(() =>
            historyService.GetCompletedPageAsync(0, PageSize + 1));
        Workouts = new ObservableCollection<HistoryWorkoutListItem>(
            sessions
                .Take(PageSize)
                .Select(session => new HistoryWorkoutListItem(session, historyService)));
        _hasMore = sessions.Count > PageSize;

        IsEmpty = Workouts.Count == 0;
    }, AppText.FailedToLoadHistory);

    [RelayCommand]
    private async Task LoadMoreAsync()
    {
        if (!_hasMore || IsBusy || IsLoadingMore)
        {
            return;
        }

        try
        {
            IsLoadingMore = true;
            ErrorMessage = null;

            var sessions = await Task.Run(() =>
                historyService.GetCompletedPageAsync(Workouts.Count, PageSize + 1));
            foreach (var session in sessions.Take(PageSize))
            {
                Workouts.Add(new HistoryWorkoutListItem(session, historyService));
            }

            _hasMore = sessions.Count > PageSize;
        }
        catch (Exception exception)
        {
            Debug.WriteLine(exception);
            ErrorMessage = AppText.FailedToLoadHistory;
        }
        finally
        {
            IsLoadingMore = false;
        }
    }

    [RelayCommand]
    private Task RetryAsync() => LoadAsync();

    [RelayCommand]
    private Task OpenWorkoutAsync(HistoryWorkoutListItem workout) =>
        navigationService.GoToAsync(
            NavigationRoutes.WorkoutDetails,
            new Dictionary<string, object> { ["WorkoutSessionId"] = workout.Id });
}
