using System;
using System.Collections.Generic;
using System.Globalization;

namespace MiHAva.UI.Services;

/// <summary>
/// MIoT Spec 属性量纲与常用单位换算工具（对齐上游 0.5.0 规范）
/// </summary>
public static class MetricFormatter
{
    private static readonly Dictionary<string, string> UnitByProp = new(StringComparer.OrdinalIgnoreCase)
    {
        ["temperature"] = "°C",
        ["relative-humidity"] = "%",
        ["humidity"] = "%",
        ["battery-level"] = "%",
        ["power"] = "W",
        ["electric-power"] = "W",
        ["voltage"] = "V",
        ["electric-current"] = "A",
        ["power-consumption"] = "kWh",
        ["pm2.5-density"] = "μg/m³",
        ["pm2.5"] = "μg/m³",
        ["co2-density"] = "ppm",
        ["co2"] = "ppm",
        ["illumination"] = "lx"
    };

    /// <summary>
    /// 根据属性名/描述与原始值进行量纲启发式修正并格式化
    /// </summary>
    public static string FormatMetric(string propNameOrDesc, double value, string? customUnit = null)
    {
        string key = propNameOrDesc.ToLowerInvariant();

        // 1. 温度：SI 规范常用 0.1 步进的整数存储（例如 283 代表 28.3°C）
        if (key.Contains("temperature") || key.Contains("温度"))
        {
            if (Math.Abs(value) > 60)
            {
                value /= 10.0;
            }
            return $"{value:F1}°C";
        }

        // 2. 相对湿度：部分设备上报 683 代表 68.3%
        if (key.Contains("humidity") || key.Contains("湿度"))
        {
            if (value > 100)
            {
                value /= 10.0;
            }
            return $"{Math.Round(value):F0}%";
        }

        // 3. 电池电量
        if (key.Contains("battery") || key.Contains("电量"))
        {
            return $"{Math.Round(value):F0}%";
        }

        // 4. 实时功率
        if (key.Equals("power", StringComparison.OrdinalIgnoreCase) ||
            key.Equals("electric-power", StringComparison.OrdinalIgnoreCase) ||
            key.Contains("功率"))
        {
            return $"{value:F1}W";
        }

        // 5. 累计用电量
        if (key.Contains("power-consumption") || key.Contains("耗电") || key.Contains("用电量"))
        {
            return $"{value:F2}kWh";
        }

        // 6. 电压
        if (key.Contains("voltage") || key.Contains("电压"))
        {
            return $"{value:F1}V";
        }

        // 7. 电流
        if (key.Contains("current") || key.Contains("电流"))
        {
            return $"{value:F2}A";
        }

        // 8. 空气指标 (PM2.5 / CO2 / TVOC)
        if (key.Contains("pm2.5"))
        {
            return $"{Math.Round(value):F0} μg/m³";
        }
        if (key.Contains("co2"))
        {
            return $"{Math.Round(value):F0} ppm";
        }

        // 9. 照度 / 光照
        if (key.Contains("illumination") || key.Contains("光照") || key.Contains("照度"))
        {
            return $"{Math.Round(value):F0} lx";
        }

        // 其他根据已知单位表或传入单位匹配
        string unit = customUnit ?? "";
        if (string.IsNullOrEmpty(unit))
        {
            foreach (var kvp in UnitByProp)
            {
                if (key.Contains(kvp.Key))
                {
                    unit = kvp.Value;
                    break;
                }
            }
        }

        if (string.IsNullOrEmpty(unit))
        {
            return value.ToString("G", CultureInfo.InvariantCulture);
        }

        return $"{value:F1} {unit}".Trim();
    }
}
