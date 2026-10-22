using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.iOS;
using Foundation;
using MiHAva.UI;
using MiHAva.UI.ViewModels;
using MiHAva.UI.Views;
using UIKit;

namespace MiHAva.iOS;

[Register("AppDelegate")]
public class AppDelegate : AvaloniaAppDelegate<iOSApp>
{
    protected override AppBuilder CustomizeAppBuilder(AppBuilder builder)
    {
        return base.CustomizeAppBuilder(builder)
            .WithInterFont();
    }
}

public partial class iOSApp : MiHAvaApplication
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
