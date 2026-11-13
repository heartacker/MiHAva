using System;
using System.Reflection;
using System.Runtime.InteropServices;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MiHAva.UI.Services;

namespace MiHAva.UI.ViewModels;

public partial class AboutViewModel : ObservableObject
{
    public string AppTitle => "MiHAva";

    public string SubTitle => "跨平台高性能米家桌面客户端";

    public string Description => "基于 .NET 11 + Avalonia + AtomUI 构建的现代化跨平台米家智能家居工作台，支持原生 Native AOT 极速启动、局域网直连与全域设备看板。";

    public string Version { get; }

    public string RuntimeInfo { get; }

    public string OsDescription { get; }

    public string Copyright { get; }

    public string LicenseText => "开源协议: Apache-2.0 / MIT License";

    public string GitHubUrl => "https://github.com/heartacker/MiHAva";

    public string ReleasesUrl => "https://github.com/heartacker/MiHAva/releases";

    public string IssuesUrl => "https://github.com/heartacker/MiHAva/issues";

    public event Action? RequestClose;

    public AboutViewModel()
    {
        // 动态读取版本号
        var assembly = typeof(AboutViewModel).Assembly;
        var infoVersion = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (!string.IsNullOrWhiteSpace(infoVersion))
        {
            var plusIdx = infoVersion.IndexOf('+');
            var cleanVer = plusIdx > 0 ? infoVersion[..plusIdx] : infoVersion;
            Version = cleanVer.StartsWith('v') ? cleanVer : $"v{cleanVer}";
        }
        else
        {
            var v = assembly.GetName().Version;
            Version = v != null ? $"v{v.Major}.{v.Minor}.{v.Build}" : "v1.0.0";
        }

        // 运行时与架构信息
        var arch = RuntimeInformation.ProcessArchitecture.ToString().ToLowerInvariant();
        RuntimeInfo = $"{RuntimeInformation.FrameworkDescription} (Native AOT / {arch}) · Avalonia 12.1.6";
        OsDescription = RuntimeInformation.OSDescription;

        // 版权信息
        var copyrightAttr = assembly.GetCustomAttribute<AssemblyCopyrightAttribute>();
        Copyright = copyrightAttr?.Copyright ?? "Copyright © 2026 heartacker & MiHAva Contributors";
    }

    [RelayCommand]
    private void OpenGitHub()
    {
        BrowserLauncher.OpenUrl(GitHubUrl);
    }

    [RelayCommand]
    private void OpenReleases()
    {
        BrowserLauncher.OpenUrl(ReleasesUrl);
    }

    [RelayCommand]
    private void OpenIssues()
    {
        BrowserLauncher.OpenUrl(IssuesUrl);
    }

    [RelayCommand]
    private void Close()
    {
        RequestClose?.Invoke();
    }
}
