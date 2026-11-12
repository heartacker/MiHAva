using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace MiHAva.UI.Services;

/// <summary>
/// 设备本地配置与重命名存储服务（兼容上游 ~/.config/mijia-api/ 文件规范）
/// </summary>
public static class DeviceSettingsService
{
    private static readonly string ConfigDir;
    private static readonly string SettingsFile;
    private static readonly string DeviceNamesFile;

    private static readonly ConcurrentDictionary<string, string> NameOverrides = new(StringComparer.OrdinalIgnoreCase);
    private static readonly object FileLock = new();

    private static bool _hideOfflineDevices;

    static DeviceSettingsService()
    {
        string userHome = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        ConfigDir = Path.Combine(userHome, ".config", "mijia-api");
        SettingsFile = Path.Combine(ConfigDir, "settings.json");
        DeviceNamesFile = Path.Combine(ConfigDir, "device_names.json");

        LoadInitialData();
    }

    public static bool HideOfflineDevices
    {
        get => _hideOfflineDevices;
        set
        {
            if (_hideOfflineDevices != value)
            {
                _hideOfflineDevices = value;
                _ = SaveSettingsAsync();
            }
        }
    }

    private static void LoadInitialData()
    {
        try
        {
            if (!Directory.Exists(ConfigDir))
            {
                Directory.CreateDirectory(ConfigDir);
            }

            // 读取 settings.json
            if (File.Exists(SettingsFile))
            {
                string json = File.ReadAllText(SettingsFile);
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("hide_offline_devices", out var prop))
                {
                    _hideOfflineDevices = prop.GetBoolean();
                }
            }

            // 读取 device_names.json
            if (File.Exists(DeviceNamesFile))
            {
                string namesJson = File.ReadAllText(DeviceNamesFile);
                var dict = JsonSerializer.Deserialize<Dictionary<string, string>>(namesJson);
                if (dict != null)
                {
                    foreach (var kvp in dict)
                    {
                        if (!string.IsNullOrWhiteSpace(kvp.Value))
                        {
                            NameOverrides[kvp.Key] = kvp.Value.Trim();
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[DeviceSettingsService] Load error: {ex.Message}");
        }
    }

    /// <summary>
    /// 获取设备的自定义名称；未配置则返回 null
    /// </summary>
    public static string? GetCustomName(string did)
    {
        return NameOverrides.TryGetValue(did, out var name) ? name : null;
    }

    /// <summary>
    /// 设置设备自定义名称；传入空串或 null 表示恢复默认
    /// </summary>
    public static void SetCustomName(string did, string? newName)
    {
        string? clean = newName?.Trim();
        if (string.IsNullOrWhiteSpace(clean))
        {
            NameOverrides.TryRemove(did, out _);
        }
        else
        {
            NameOverrides[did] = clean;
        }

        _ = SaveDeviceNamesAsync();
    }

    private static Task SaveSettingsAsync()
    {
        return Task.Run(() =>
        {
            lock (FileLock)
            {
                try
                {
                    if (!Directory.Exists(ConfigDir)) Directory.CreateDirectory(ConfigDir);
                    Dictionary<string, object> data = [];
                    if (File.Exists(SettingsFile))
                    {
                        try
                        {
                            var existing = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(File.ReadAllText(SettingsFile));
                            if (existing != null)
                            {
                                foreach (var kv in existing)
                                {
                                    data[kv.Key] = kv.Value;
                                }
                            }
                        }
                        catch
                        {
                        }
                    }

                    data["hide_offline_devices"] = _hideOfflineDevices;

                    string updatedJson = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
                    File.WriteAllText(SettingsFile, updatedJson);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[DeviceSettingsService] SaveSettings error: {ex.Message}");
                }
            }
        });
    }

    private static Task SaveDeviceNamesAsync()
    {
        return Task.Run(() =>
        {
            lock (FileLock)
            {
                try
                {
                    if (!Directory.Exists(ConfigDir)) Directory.CreateDirectory(ConfigDir);
                    var dict = new Dictionary<string, string>(NameOverrides);
                    string json = JsonSerializer.Serialize(dict, new JsonSerializerOptions { WriteIndented = true });
                    File.WriteAllText(DeviceNamesFile, json);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[DeviceSettingsService] SaveDeviceNames error: {ex.Message}");
                }
            }
        });
    }
}
