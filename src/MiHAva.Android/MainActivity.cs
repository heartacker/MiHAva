using Android.App;
using Android.Content.PM;
using Avalonia;
using Avalonia.Android;
using MiHAva.UI;
using MiHAva.UI.ViewModels;
using MiHAva.UI.Views;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;

namespace MiHAva.Android;

[Activity(
    Label = "米家客户端",
    Theme = "@style/MyTheme.NoActionBar",
    Icon = "@drawable/icon",
    MainLauncher = true,
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.UiMode)]
public class MainActivity : AvaloniaMainActivity<AndroidApp>
{
    protected override AppBuilder CustomizeAppBuilder(AppBuilder builder)
    {
        return base.CustomizeAppBuilder(builder)
            .WithInterFont();
    }
}

public partial class AndroidApp : MiHAvaApplication
{
    public override async void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is ISingleViewApplicationLifetime singleView)
        {
            var root = new ContentControl();
            singleView.MainView = root;

            var initialVm = await ResolveInitialViewModelAsync(onLoggedIn: mainVm =>
            {
                root.Content = new MainView { DataContext = mainVm };
            });

            if (initialVm is MainViewModel mainVm)
            {
                root.Content = new MainView { DataContext = mainVm };
            }
            else if (initialVm is LoginViewModel loginVm)
            {
                root.Content = new LoginView { DataContext = loginVm };
            }
        }

        base.OnFrameworkInitializationCompleted();
    }
}
