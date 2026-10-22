using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MiJia.Net.Models.Devices;
using MiJia.Net.Serialization;

namespace MiHAva.UI.Services;

public sealed class CachedHomeData
{
    public List<HomeInfo> Homes { get; set; } = [];
    public List<DeviceInfo> Devices { get; set; } = [];
    public DateTimeOffset CachedAt { get; set; } = DateTimeOffset.UtcNow;
}

public static class DeviceCacheService
{
    private static string GetCacheFilePath()
    {
        string userHome = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        string configDir = Path.Combine(userHome, ".config", "mijia-api");
        return Path.Combine(configDir, "devices_cache.json");
    }

    /// <summary>
    /// 读取本地缓存的家庭与设备数据（秒开加载）
    /// </summary>
    public static async Task<CachedHomeData?> LoadCacheAsync(CancellationToken cancellationToken = default)
    {
        string path = GetCacheFilePath();
        if (!File.Exists(path)) return null;

        try
        {
            string json = await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var result = new CachedHomeData();
            if (root.TryGetProperty("homes", out var homesEl))
            {
                var homes = JsonSerializer.Deserialize(homesEl.GetRawText(), MiJiaJsonContext.Default.ListHomeInfo);
                if (homes != null) result.Homes = homes;
            }

            if (root.TryGetProperty("devices", out var devsEl))
            {
                var devs = JsonSerializer.Deserialize(devsEl.GetRawText(), MiJiaJsonContext.Default.ListDeviceInfo);
                if (devs != null) result.Devices = devs;
            }

            return result;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// 清除本地缓存的设备数据与家庭信息（退出登录时调用）
    /// </summary>
    public static void ClearCache()
    {
        try
        {
            string path = GetCacheFilePath();
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
        }
    }
    public static async Task SaveCacheAsync(List<HomeInfo> homes, List<DeviceInfo> devices, CancellationToken cancellationToken = default)
    {
        string path = GetCacheFilePath();
        string dir = Path.GetDirectoryName(path)!;
        if (!Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        try
        {
            using var stream = File.Create(path);
            using var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true });

            writer.WriteStartObject();
            writer.WriteString("cachedAt", DateTimeOffset.UtcNow.ToString("O"));

            writer.WritePropertyName("homes");
            JsonSerializer.Serialize(writer, homes, MiJiaJsonContext.Default.ListHomeInfo);

            writer.WritePropertyName("devices");
            JsonSerializer.Serialize(writer, devices, MiJiaJsonContext.Default.ListDeviceInfo);

            writer.WriteEndObject();
            await writer.FlushAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            // 缓存保存异常不打断主业务流程
        }
    }
}
