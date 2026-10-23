using Android.App;
using Android.Content.PM;
using Avalonia;
using Avalonia.Android;
using Avalonia.Controls.ApplicationLifetimes;
using MiHAva.UI;
using MiHAva.UI.Views;

namespace MiHAva.Android;

[Activity(
    Label = "米家客户端",
    Theme = "@style/MyTheme.NoActionBar",
    Icon = "@drawable/icon",
    MainLauncher = true,
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.UiMode)]
public class MainActivity : AvaloniaMainActivity<AndroidApp>
{
    private static SingleViewShell? _activeShell;

    internal static void RegisterShell(SingleViewShell shell)
    {
        _activeShell = shell;
    }

    protected override AppBuilder CustomizeAppBuilder(AppBuilder builder)
    {
        return base.CustomizeAppBuilder(builder)
            .WithInterFont();
    }

    public override void OnBackPressed()
    {
        // 优先关闭弹窗/详情蒙层
        if (_activeShell != null && _activeShell.HandleBackButton())
        {
            return;
        }

        base.OnBackPressed();
    }
}

public partial class AndroidApp : MiHAvaApplication
{
    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is ISingleViewApplicationLifetime singleView)
        {
            var shell = new SingleViewShell();
            MainActivity.RegisterShell(shell);
            singleView.MainView = shell;
            _ = shell.InitializeAsync();
        }

        base.OnFrameworkInitializationCompleted();
    }
}
