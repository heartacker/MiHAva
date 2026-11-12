using System;
using System.Collections.Generic;
using System.Globalization;

namespace MiHAva.UI.Services;

/// <summary>
/// MIoT Spec 属性量纲与常用单位换算工具（对齐上游 0.5.0 规范并精确区分色温与环境温湿度）
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
    /// 判断属性是否为真实环境温度（精确排除灯光色温、设定/目标温度、水温）
    /// </summary>
    public static bool IsEnvironmentTemperature(string propNameOrDesc)
    {
        string key = propNameOrDesc.ToLowerInvariant();
        return (key.Contains("temperature") || key.Contains("温度") || key.Contains("室温")) &&
               !key.Contains("color") && !key.Contains("色温") &&
               !key.Contains("target") && !key.Contains("目标") &&
               !key.Contains("setting") && !key.Contains("设定") &&
               !key.Contains("water") && !key.Contains("水温");
    }

    /// <summary>
    /// 判断属性是否为相对湿度（排除目标设定湿度）
    /// </summary>
    public static bool IsHumidity(string propNameOrDesc)
    {
        string key = propNameOrDesc.ToLowerInvariant();
        return (key.Contains("humidity") || key.Contains("湿度")) &&
               !key.Contains("target") && !key.Contains("目标") && !key.Contains("设定");
    }

    /// <summary>
    /// 根据属性名/描述与原始值进行量纲启发式修正并格式化
    /// </summary>
    public static string FormatMetric(string propNameOrDesc, double value, string? customUnit = null)
    {
        string key = propNameOrDesc.ToLowerInvariant();

        // 1. 色温（Color Temperature）：单位为 K，绝不能被当作环境摄氏度！
        if (key.Contains("color-temperature") || key.Contains("color_temperature") || key.Contains("色温"))
        {
            if (value > 10 && value <= 100)
            {
                value *= 100;
            }
            return $"{Math.Round(value):F0}K";
        }

        // 2. 环境温度：排除目标温度与水温，合理室温一般在 -40°C ~ 60°C 之间
        if (IsEnvironmentTemperature(key))
        {
            if (Math.Abs(value) > 60 && Math.Abs(value) < 1000)
            {
                value /= 10.0;
            }
            else if (Math.Abs(value) >= 1000)
            {
                value /= 100.0;
            }
            return $"{value:F1}°C";
        }

        // 3. 相对湿度：部分设备上报 683 代表 68.3%
        if (IsHumidity(key))
        {
            if (value > 100 && value < 1000)
            {
                value /= 10.0;
            }
            else if (value >= 1000)
            {
                value /= 100.0;
            }
            return $"{Math.Round(value):F0}%";
        }

        // 4. 电池电量
        if (key.Contains("battery") || key.Contains("电量"))
        {
            return $"{Math.Round(value):F0}%";
        }

        // 5. 实时功率
        if (key.Equals("power", StringComparison.OrdinalIgnoreCase) ||
            key.Equals("electric-power", StringComparison.OrdinalIgnoreCase) ||
            key.Contains("功率"))
        {
            return $"{value:F1}W";
        }

        // 6. 累计用电量
        if (key.Contains("power-consumption") || key.Contains("耗电") || key.Contains("用电量"))
        {
            return $"{value:F2}kWh";
        }

        // 7. 电压
        if (key.Contains("voltage") || key.Contains("电压"))
        {
            return $"{value:F1}V";
        }

        // 8. 电流
        if (key.Contains("current") || key.Contains("电流"))
        {
            return $"{value:F2}A";
        }

        // 9. 空气指标 (PM2.5 / CO2 / TVOC)
        if (key.Contains("pm2.5"))
        {
            return $"{Math.Round(value):F0} μg/m³";
        }
        if (key.Contains("co2"))
        {
            return $"{Math.Round(value):F0} ppm";
        }

        // 10. 照度 / 光照
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
