using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace MiHAva.UI.Services;

/// <summary>
/// 跨平台网页浏览器打开服务
/// </summary>
public static class BrowserLauncher
{
    public static void OpenUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url)) return;

        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                Process.Start("xdg-open", url);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                Process.Start("open", url);
            }
            else
            {
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[BrowserLauncher] 打开 URL 失败 ({url}): {ex.Message}");
        }
    }
}
