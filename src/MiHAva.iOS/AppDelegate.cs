using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.iOS;
using Foundation;
using MiHAva.UI;
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
    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is ISingleViewApplicationLifetime singleView)
        {
            var shell = new SingleViewShell();
            singleView.MainView = shell;
            _ = shell.InitializeAsync();
        }

        base.OnFrameworkInitializationCompleted();
    }
}
