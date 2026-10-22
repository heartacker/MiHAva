using System;
using AtomUI;
using AtomUI.Desktop.Controls;
using AtomUI.Fonts.AlibabaSans;
using AtomUI.Theme;
using Avalonia;

namespace MiHAva.Desktop;

internal sealed class Program
{
    [STAThread]
    public static void Main(string[] args) => BuildAvaloniaApp()
        .StartWithClassicDesktopLifetime(args);

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<DesktopApp>()
            .UseAtomUIPlatformDetect()
            .WithAtomUIDefaultOptions()
            .UseAtomUI(builder =>
            {
                builder.WithApplicationId("MiHAva.Desktop")
                       .UseAlibabaSansFont()
                       .UseDesktopControls()
                       .WithInitialTheme(IThemeManager.DEFAULT_THEME_ID);
            })
            .LogToTrace();
}
