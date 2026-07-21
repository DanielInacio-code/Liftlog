namespace LiftLog.App.Services;

public sealed class ShellNavigationService : INavigationService
{
    public async Task GoToAsync(string route, IDictionary<string, object>? parameters = null)
    {
        var animate = !string.Equals(
            route,
            NavigationRoutes.ActiveWorkout,
            StringComparison.Ordinal);
        if (parameters is null)
        {
            await Shell.Current.GoToAsync(route, animate);
        }
        else
        {
            await Shell.Current.GoToAsync(
                route,
                animate,
                new ShellNavigationQueryParameters(parameters));
        }

    }

    public Task GoBackAsync() => Shell.Current.GoToAsync("..");
}
