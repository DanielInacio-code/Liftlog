namespace LiftLog.App.Controls;

public sealed record ChartDataPoint(string Label, double Value);

public enum TrainingChartKind
{
    Bars,
    Line
}
