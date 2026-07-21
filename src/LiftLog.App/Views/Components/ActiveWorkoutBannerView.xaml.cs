using LiftLog.App.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace LiftLog.App.Views.Components;

public partial class ActiveWorkoutBannerView : ContentView
{
    private ActiveWorkoutBannerViewModel? _viewModel;
    private bool _isActive;

    public ActiveWorkoutBannerView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    protected override void OnHandlerChanged()
    {
        base.OnHandlerChanged();
        ResolveViewModel();
    }

    public Task RefreshAsync()
    {
        ResolveViewModel();
        return _viewModel?.RefreshAsync() ?? Task.CompletedTask;
    }

    public Task EnsureCurrentAsync()
    {
        ResolveViewModel();
        return _viewModel?.EnsureLoadedAsync() ?? Task.CompletedTask;
    }

    private void ResolveViewModel()
    {
        if (_viewModel is not null || Handler?.MauiContext?.Services is not { } services)
        {
            return;
        }

        _viewModel = services.GetRequiredService<ActiveWorkoutBannerViewModel>();
        BindingContext = _viewModel;
    }

    private async void OnLoaded(object? sender, EventArgs eventArgs)
    {
        ResolveViewModel();
        if (_viewModel is null)
        {
            return;
        }

        if (!_isActive)
        {
            _viewModel.Activate();
            _isActive = true;
        }

        await _viewModel.EnsureLoadedAsync();
    }

    private void OnUnloaded(object? sender, EventArgs eventArgs)
    {
        if (!_isActive)
        {
            return;
        }

        _viewModel?.Deactivate();
        _isActive = false;
    }
}
