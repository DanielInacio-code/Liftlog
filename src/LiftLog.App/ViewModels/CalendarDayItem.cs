namespace LiftLog.App.ViewModels;

public sealed record CalendarDayItem(
    DateOnly Date,
    bool IsCurrentMonth,
    bool HasWorkout,
    bool IsSelected)
{
    public string DayNumber => Date.Day.ToString();
}
