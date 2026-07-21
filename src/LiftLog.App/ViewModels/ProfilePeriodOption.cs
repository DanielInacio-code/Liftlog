namespace LiftLog.App.ViewModels;

public enum ProfilePeriod
{
    LastMonth,
    ThreeMonths,
    OneYear
}

public sealed record ProfilePeriodOption(string Name, ProfilePeriod Period);

public enum TrainingMetric
{
    Volume,
    Duration,
    Sets
}
