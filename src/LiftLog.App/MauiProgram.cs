using LiftLog.App.Controls;
using LiftLog.App.ViewModels;
using LiftLog.App.Views;
using LiftLog.App.Services;
using LiftLog.Core.Data;
using LiftLog.Core.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LiftLog.App;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        SQLitePCL.Batteries.Init();

        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

#if ANDROID
        Microsoft.Maui.Handlers.EntryHandler.Mapper.AppendToMapping(
            nameof(DecimalEntry),
            (handler, entry) =>
            {
                if (entry is not DecimalEntry)
                {
                    return;
                }

                handler.PlatformView.InputType =
                    Android.Text.InputTypes.ClassNumber |
                    Android.Text.InputTypes.NumberFlagDecimal;
                handler.PlatformView.KeyListener =
                    Android.Text.Method.DigitsKeyListener.GetInstance("0123456789.,");
            });
#endif

#if DEBUG
        builder.Logging.AddDebug();
#endif

        var databasePath = Path.Combine(FileSystem.AppDataDirectory, "liftlog.db3");
        builder.Services.AddDbContextFactory<LiftLogDbContext>(options =>
            options.UseSqlite($"Data Source={databasePath}"));

        builder.Services.AddSingleton<IDatabaseInitializer, DatabaseInitializer>();
        builder.Services.AddSingleton<IExerciseService, ExerciseService>();
        builder.Services.AddSingleton<IRoutineService, RoutineService>();
        builder.Services.AddSingleton<IWorkoutService, WorkoutService>();
        builder.Services.AddSingleton<IHistoryService, HistoryService>();
        builder.Services.AddSingleton<IStatisticsService, StatisticsService>();
        builder.Services.AddSingleton<IBodyMeasurementService, BodyMeasurementService>();
        builder.Services.AddSingleton<INavigationService, ShellNavigationService>();
        builder.Services.AddSingleton<IExerciseImageService, ExerciseImageService>();
        builder.Services.AddSingleton<WorkoutHubState>();

        builder.Services.AddTransient<AppShell>();
        builder.Services.AddTransient<HomeViewModel>();
        builder.Services.AddTransient<HomePage>();
        builder.Services.AddTransient<ExercisesViewModel>();
        builder.Services.AddTransient<ExercisesPage>();
        builder.Services.AddTransient<RoutinesViewModel>();
        builder.Services.AddTransient<RoutinesPage>();
        builder.Services.AddTransient<RoutineEditViewModel>();
        builder.Services.AddTransient<RoutineEditPage>();
        builder.Services.AddTransient<ActiveWorkoutViewModel>();
        builder.Services.AddSingleton<ActiveWorkoutBannerViewModel>();
        builder.Services.AddTransient<ActiveWorkoutPage>();
        builder.Services.AddTransient<HistoryViewModel>();
        builder.Services.AddTransient<HistoryPage>();
        builder.Services.AddTransient<ProgressViewModel>();
        builder.Services.AddTransient<ProgressPage>();
        builder.Services.AddTransient<ProfileViewModel>();
        builder.Services.AddTransient<ProfilePage>();
        builder.Services.AddTransient<CalendarViewModel>();
        builder.Services.AddTransient<CalendarPage>();
        builder.Services.AddTransient<MeasurementsViewModel>();
        builder.Services.AddTransient<MeasurementsPage>();
        builder.Services.AddTransient<SettingsViewModel>();
        builder.Services.AddTransient<SettingsPage>();
        builder.Services.AddTransient<DataBackupViewModel>();
        builder.Services.AddTransient<DataBackupPage>();
        builder.Services.AddTransient<WorkoutSummaryViewModel>();
        builder.Services.AddTransient<WorkoutSummaryPage>();
        builder.Services.AddTransient<WorkoutDetailsViewModel>();
        builder.Services.AddTransient<WorkoutDetailsPage>();
        builder.Services.AddTransient<ExerciseDetailsViewModel>();
        builder.Services.AddTransient<ExerciseDetailsPage>();
        builder.Services.AddTransient<ExerciseEditViewModel>();
        builder.Services.AddTransient<ExerciseEditPage>();
        builder.Services.AddTransient<ExercisePickerViewModel>();
        builder.Services.AddTransient<ExercisePickerPage>();

        return builder.Build();
    }
}
