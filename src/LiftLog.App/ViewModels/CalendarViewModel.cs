using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LiftLog.App.Resources.Strings;
using LiftLog.App.Services;
using LiftLog.Core.Models;
using LiftLog.Core.Services;

namespace LiftLog.App.ViewModels;

public partial class CalendarViewModel(
    IHistoryService historyService,
    INavigationService navigationService) : BaseViewModel
{
    private DateOnly _displayedMonth = new(DateTime.Today.Year, DateTime.Today.Month, 1);
    private DateOnly _selectedDate = DateOnly.FromDateTime(DateTime.Today);
    private IReadOnlyList<WorkoutSession> _monthWorkouts = [];

    [ObservableProperty]
    private string monthTitle = string.Empty;

    [ObservableProperty]
    private string selectedDayTitle = string.Empty;

    [ObservableProperty]
    private IReadOnlyList<CalendarDayItem> days = [];

    [ObservableProperty]
    private ObservableCollection<HistoryWorkoutListItem> selectedWorkouts = [];

    [ObservableProperty]
    private bool hasSelectedWorkouts;

    [ObservableProperty]
    private bool hasNoSelectedWorkouts = true;

    public Task LoadAsync() => LoadMonthAsync();

    [RelayCommand]
    private Task PreviousMonthAsync()
    {
        _displayedMonth = _displayedMonth.AddMonths(-1);
        return LoadMonthAsync();
    }

    [RelayCommand]
    private Task NextMonthAsync()
    {
        _displayedMonth = _displayedMonth.AddMonths(1);
        return LoadMonthAsync();
    }

    [RelayCommand]
    private void SelectDay(CalendarDayItem day)
    {
        if (!day.IsCurrentMonth)
        {
            return;
        }

        _selectedDate = day.Date;
        RebuildCalendar();
        RefreshSelectedWorkouts();
    }

    [RelayCommand]
    private Task OpenWorkoutAsync(HistoryWorkoutListItem workout) =>
        navigationService.GoToAsync(
            NavigationRoutes.WorkoutDetails,
            new Dictionary<string, object> { ["WorkoutSessionId"] = workout.Id });

    [RelayCommand]
    private Task RetryAsync() => LoadMonthAsync();

    private Task LoadMonthAsync() => RunBusyAsync(async () =>
    {
        var localOffset = DateTimeOffset.Now.Offset;
        var start = new DateTimeOffset(
            _displayedMonth.Year,
            _displayedMonth.Month,
            1,
            0,
            0,
            0,
            localOffset);
        var end = start.AddMonths(1).AddTicks(-1);
        _monthWorkouts = await historyService.GetCompletedAsync(start, end);

        var today = DateOnly.FromDateTime(DateTime.Today);
        _selectedDate = today.Year == _displayedMonth.Year && today.Month == _displayedMonth.Month
            ? today
            : _monthWorkouts
                .Select(workout => DateOnly.FromDateTime(workout.StartedAt.LocalDateTime))
                .OrderByDescending(date => date)
                .FirstOrDefault(_displayedMonth);

        MonthTitle = start.ToString("MMMM yyyy");
        RebuildCalendar();
        RefreshSelectedWorkouts();
    }, AppText.FailedToLoadCalendar);

    private void RebuildCalendar()
    {
        var firstDay = new DateOnly(_displayedMonth.Year, _displayedMonth.Month, 1);
        var daysSinceMonday = ((int)firstDay.DayOfWeek + 6) % 7;
        var gridStart = firstDay.AddDays(-daysSinceMonday);
        var workoutDates = _monthWorkouts
            .Select(workout => DateOnly.FromDateTime(workout.StartedAt.LocalDateTime))
            .ToHashSet();

        Days = Enumerable.Range(0, 42)
            .Select(offset =>
            {
                var date = gridStart.AddDays(offset);
                return new CalendarDayItem(
                    date,
                    date.Month == _displayedMonth.Month && date.Year == _displayedMonth.Year,
                    workoutDates.Contains(date),
                    date == _selectedDate);
            })
            .ToList();
    }

    private void RefreshSelectedWorkouts()
    {
        SelectedDayTitle = _selectedDate.ToString("dddd, dd MMMM");
        SelectedWorkouts = new ObservableCollection<HistoryWorkoutListItem>(
            _monthWorkouts
                .Where(workout =>
                    DateOnly.FromDateTime(workout.StartedAt.LocalDateTime) == _selectedDate)
                .OrderByDescending(workout => workout.StartedAt)
                .Select(workout => new HistoryWorkoutListItem(workout, historyService)));
        HasSelectedWorkouts = SelectedWorkouts.Count > 0;
        HasNoSelectedWorkouts = !HasSelectedWorkouts;
    }
}
