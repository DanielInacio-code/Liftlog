using LiftLog.App.Services;
using LiftLog.App.Views;
using Microsoft.Extensions.DependencyInjection;

namespace LiftLog.App;

public partial class AppShell : Shell
{
    public AppShell(IServiceProvider services)
    {
        InitializeComponent();

        HomeShellContent.ContentTemplate =
            new DataTemplate(() => services.GetRequiredService<HomePage>());
        RoutinesShellContent.ContentTemplate =
            new DataTemplate(() => services.GetRequiredService<RoutinesPage>());
        ProfileShellContent.ContentTemplate =
            new DataTemplate(() => services.GetRequiredService<ProfilePage>());

        Routing.RegisterRoute("exercises", typeof(ExercisesPage));
        Routing.RegisterRoute("history", typeof(HistoryPage));
        Routing.RegisterRoute(NavigationRoutes.ExerciseProgress, typeof(ProgressPage));
        Routing.RegisterRoute(NavigationRoutes.Calendar, typeof(CalendarPage));
        Routing.RegisterRoute(NavigationRoutes.Measurements, typeof(MeasurementsPage));
        Routing.RegisterRoute(NavigationRoutes.Settings, typeof(SettingsPage));
        Routing.RegisterRoute(NavigationRoutes.DataBackup, typeof(DataBackupPage));
        Routing.RegisterRoute(NavigationRoutes.RoutineEdit, typeof(RoutineEditPage));
        Routing.RegisterRoute(NavigationRoutes.ActiveWorkout, typeof(ActiveWorkoutPage));
        Routing.RegisterRoute(NavigationRoutes.WorkoutSummary, typeof(WorkoutSummaryPage));
        Routing.RegisterRoute(NavigationRoutes.WorkoutDetails, typeof(WorkoutDetailsPage));
        Routing.RegisterRoute(NavigationRoutes.ExerciseDetails, typeof(ExerciseDetailsPage));
        Routing.RegisterRoute(NavigationRoutes.ExerciseEdit, typeof(ExerciseEditPage));
        Routing.RegisterRoute(NavigationRoutes.ExercisePicker, typeof(ExercisePickerPage));
    }
}
