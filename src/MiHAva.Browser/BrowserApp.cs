using Avalonia.Controls.ApplicationLifetimes;
using MiHAva.UI;
using MiHAva.UI.Views;

namespace MiHAva.Browser;

public partial class BrowserApp : MiHAvaApplication
{
    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is ISingleViewApplicationLifetime singleViewPlatform)
        {
            var shell = new SingleViewShell();
            singleViewPlatform.MainView = shell;
            _ = shell.InitializeAsync();
        }

        base.OnFrameworkInitializationCompleted();
    }
}
