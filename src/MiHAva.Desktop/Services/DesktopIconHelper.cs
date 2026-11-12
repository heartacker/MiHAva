using System;
using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace MiHAva.Desktop.Services;

public static class DesktopIconHelper
{
    private static WindowIcon? _cachedIcon;

    public static WindowIcon? GetOrCreateAppIcon()
    {
        if (_cachedIcon != null) return _cachedIcon;

        // 1. 优先尝试从 AvaloniaResource 加载标准图标
        string[] candidateUris = [
            "avares://MiHAva.UI/Assets/icon.ico",
            "avares://MiHAva.UI/Assets/icon.png",
            "avares://MiHAva.UI/Assets/tray_icon.png"
        ];

        foreach (var uriStr in candidateUris)
        {
            try
            {
                var uri = new Uri(uriStr);
                if (AssetLoader.Exists(uri))
                {
                    using var stream = AssetLoader.Open(uri);
                    _cachedIcon = new WindowIcon(stream);
                    return _cachedIcon;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[IconHelper] Load {uriStr} failed: {ex.Message}");
            }
        }

        // 2. 尝试从本地磁盘相对路径加载
        string[] candidatePaths = [
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "icon.ico"),
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "icon.png"),
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "MiHAva.UI", "Assets", "icon.ico"),
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "MiHAva.UI", "Assets", "icon.png")
        ];

        foreach (var p in candidatePaths)
        {
            try
            {
                if (File.Exists(p))
                {
                    using var stream = File.OpenRead(p);
                    _cachedIcon = new WindowIcon(stream);
                    return _cachedIcon;
                }
            }
            catch
            {
            }
        }

        // 3. 终极内存兜底：动态创建标准 WriteableBitmap 并保存为标准 PNG 流，确保永不抛出 ArgumentException
        try
        {
            var wb = new WriteableBitmap(new PixelSize(16, 16), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Premul);
            using var frameStream = new MemoryStream();
            wb.Save(frameStream);
            frameStream.Position = 0;
            _cachedIcon = new WindowIcon(frameStream);
            return _cachedIcon;
        }
        catch
        {
        }

        return _cachedIcon;
    }
}
