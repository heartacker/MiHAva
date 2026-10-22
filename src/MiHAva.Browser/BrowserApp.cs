using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using MiHAva.UI;
using MiHAva.UI.ViewModels;
using MiHAva.UI.Views;

namespace MiHAva.Browser;

public partial class BrowserApp : MiHAvaApplication
{
    public override async void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is ISingleViewApplicationLifetime singleViewPlatform)
        {
            var rootContainer = new ContentControl();
            singleViewPlatform.MainView = rootContainer;

            var initialVm = await ResolveInitialViewModelAsync(onLoggedIn: mainVm =>
            {
                rootContainer.Content = new MainView { DataContext = mainVm };
            });

            if (initialVm is MainViewModel mainVm)
            {
                rootContainer.Content = new MainView { DataContext = mainVm };
            }
            else if (initialVm is LoginViewModel loginVm)
            {
                rootContainer.Content = new LoginView { DataContext = loginVm };
            }
        }

        base.OnFrameworkInitializationCompleted();
    }
}
