using CommunityToolkit.Mvvm.ComponentModel;
using System.Diagnostics;

namespace LiftLog.App.ViewModels;

public abstract partial class BaseViewModel : ObservableObject
{
    [ObservableProperty]
    private string title = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotBusy))]
    [NotifyPropertyChangedFor(nameof(IsInitialLoading))]
    private bool isBusy;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    private string? errorMessage;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsInitialLoading))]
    private bool hasLoaded;

    public bool IsNotBusy => !IsBusy;

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

    public bool IsInitialLoading => IsBusy && !HasLoaded;

    protected async Task RunBusyAsync(Func<Task> operation, string friendlyErrorMessage)
    {
        if (IsBusy)
        {
            return;
        }

        try
        {
            IsBusy = true;
            ErrorMessage = null;
            await operation();
        }
        catch (Exception exception)
        {
            Debug.WriteLine(exception);
            ErrorMessage = friendlyErrorMessage;
        }
        finally
        {
            HasLoaded = true;
            IsBusy = false;
        }
    }
}
