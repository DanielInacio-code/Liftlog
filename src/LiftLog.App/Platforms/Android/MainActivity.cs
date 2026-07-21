using Android.App;
using Android.Content;
using Android.Content.Res;
using Android.Content.PM;
using Android.OS;
using AndroidLocale = Java.Util.Locale;

namespace LiftLog.App;

[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTop, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
    protected override void AttachBaseContext(Context? @base)
    {
        ArgumentNullException.ThrowIfNull(@base);

        var locale = AndroidLocale.ForLanguageTag("en-GB");
        AndroidLocale.Default = locale;

        var configuration = new Configuration(@base.Resources?.Configuration);
        configuration.SetLocale(locale);
        configuration.SetLayoutDirection(locale);

        base.AttachBaseContext(@base.CreateConfigurationContext(configuration));
    }
}
