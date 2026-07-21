using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LiftLog.App.Controls;
using LiftLog.App.Resources.Strings;
using LiftLog.Core.Services;

namespace LiftLog.App.ViewModels;

public partial class MeasurementsViewModel(IBodyMeasurementService measurementService) : BaseViewModel
{
    [ObservableProperty]
    private ObservableCollection<BodyMeasurementListItem> measurements = [];

    [ObservableProperty]
    private IReadOnlyList<ChartDataPoint> weightChartPoints = [];

    [ObservableProperty]
    private bool hasMeasurements;

    [ObservableProperty]
    private bool hasNoMeasurements = true;

    [ObservableProperty]
    private bool hasWeightTrend;

    [ObservableProperty]
    private bool isEditorVisible;

    [ObservableProperty]
    private DateTime selectedDate = DateTime.Today;

    [ObservableProperty]
    private string weightText = string.Empty;

    [ObservableProperty]
    private string bodyFatText = string.Empty;

    [ObservableProperty]
    private string waistText = string.Empty;

    [ObservableProperty]
    private string notes = string.Empty;

    public Task LoadAsync() => RunBusyAsync(LoadCoreAsync, AppText.FailedToLoadMeasurements);

    [RelayCommand]
    private void ShowEditor()
    {
        ErrorMessage = null;
        IsEditorVisible = true;
    }

    [RelayCommand]
    private void CancelEditor()
    {
        ResetEditor();
        ErrorMessage = null;
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (IsBusy)
        {
            return;
        }

        if (!TryParseOptionalDecimal(WeightText, out var weight) ||
            !TryParseOptionalDecimal(BodyFatText, out var bodyFat) ||
            !TryParseOptionalDecimal(WaistText, out var waist))
        {
            ErrorMessage = AppText.InvalidMeasurementValue;
            return;
        }

        try
        {
            IsBusy = true;
            ErrorMessage = null;
            var recordedAt = new DateTimeOffset(
                SelectedDate.Year,
                SelectedDate.Month,
                SelectedDate.Day,
                12,
                0,
                0,
                DateTimeOffset.Now.Offset);
            await measurementService.CreateAsync(
                new BodyMeasurementInput(recordedAt, weight, bodyFat, waist, Notes));
            ResetEditor();
            await LoadCoreAsync();
        }
        catch (BodyMeasurementValidationException exception)
        {
            ErrorMessage = exception.Message;
        }
        catch (Exception exception)
        {
            System.Diagnostics.Debug.WriteLine(exception);
            ErrorMessage = AppText.FailedToSaveMeasurement;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task DeleteAsync(BodyMeasurementListItem measurement)
    {
        if (IsBusy)
        {
            return;
        }

        try
        {
            IsBusy = true;
            ErrorMessage = null;
            await measurementService.DeleteAsync(measurement.Id);
            await LoadCoreAsync();
        }
        catch (Exception exception)
        {
            System.Diagnostics.Debug.WriteLine(exception);
            ErrorMessage = AppText.FailedToDeleteMeasurement;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private Task RetryAsync() => LoadAsync();

    private async Task LoadCoreAsync()
    {
        var records = await measurementService.GetAllAsync();
        Measurements = new ObservableCollection<BodyMeasurementListItem>(
            records.Select(measurement => new BodyMeasurementListItem(measurement)));
        WeightChartPoints = records
            .Where(measurement => measurement.WeightKg.HasValue)
            .OrderBy(measurement => measurement.RecordedAt)
            .Select(measurement => new ChartDataPoint(
                measurement.RecordedAt.ToLocalTime().ToString("dd MMM"),
                (double)measurement.WeightKg!.Value))
            .ToList();
        HasMeasurements = Measurements.Count > 0;
        HasNoMeasurements = !HasMeasurements;
        HasWeightTrend = WeightChartPoints.Count > 0;
    }

    private void ResetEditor()
    {
        IsEditorVisible = false;
        SelectedDate = DateTime.Today;
        WeightText = string.Empty;
        BodyFatText = string.Empty;
        WaistText = string.Empty;
        Notes = string.Empty;
    }

    private static bool TryParseOptionalDecimal(string text, out decimal? value)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            value = null;
            return true;
        }

        if (decimal.TryParse(
                text.Replace(',', '.'),
                NumberStyles.Number,
                CultureInfo.InvariantCulture,
                out var parsed))
        {
            value = parsed;
            return true;
        }

        value = null;
        return false;
    }
}
