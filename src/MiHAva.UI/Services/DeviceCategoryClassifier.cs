using System;
using MiHAva.UI.ViewModels;

namespace MiHAva.UI.Services;

public enum DeviceCategoryType
{
    Lighting = 1,       // 💡 照明 / 灯
    LightStrip = 2,     // 🌈 灯带
    Speaker = 3,        // 🔊 音箱 / 音响
    Curtain = 4,        // 🪟 遮阳 / 窗帘
    Switch = 5,         // 🔌 开关 / 插座
    Environment = 6,    // 🌡️ 环境温湿度 / 空调
    Security = 7,       // 🛡️ 安防 / 传感
    Other = 8           // 📱 其他智能设备
}

public static class DeviceCategoryClassifier
{
    public static DeviceCategoryType GetCategory(DeviceCardViewModel card)
    {
        string model = card.Device.Model?.ToLowerInvariant() ?? string.Empty;
        string name = card.Name?.ToLowerInvariant() ?? string.Empty;
        string spec = card.Device.SpecType?.ToLowerInvariant() ?? string.Empty;

        // 1. 灯带（优先于普通灯，避免因名字中包含“灯”被误归类）
        if (model.Contains("strip") || model.Contains("lightstrip") ||
            name.Contains("灯带") || name.Contains("光带") || name.Contains("氛围灯") ||
            spec.Contains("light-strip"))
        {
            return DeviceCategoryType.LightStrip;
        }

        // 2. 音箱 / 音响 / 小爱 / 播放器 / 电视影音
        if (model.Contains("speaker") || model.Contains("soundbox") || model.Contains("audio") || model.Contains("xiaoai") ||
            name.Contains("音箱") || name.Contains("音响") || name.Contains("小爱") || name.Contains("播放器") || name.Contains("soundbar") ||
            spec.Contains("speaker") || spec.Contains("media-player") || spec.Contains("audio"))
        {
            return DeviceCategoryType.Speaker;
        }

        // 3. 照明 / 灯具
        if (model.Contains("light") || model.Contains("lamp") || model.Contains("ceiling") || model.Contains("downlight") ||
            name.Contains("灯") || name.Contains("筒灯") || name.Contains("射灯") || name.Contains("吸顶灯") || name.Contains("台灯") || name.Contains("吊灯") ||
            spec.Contains("light"))
        {
            return DeviceCategoryType.Lighting;
        }

        // 4. 窗帘 / 遮阳 / 晾衣架
        if (model.Contains("curtain") || model.Contains("shade") || model.Contains("blind") || model.Contains("roller") ||
            name.Contains("窗帘") || name.Contains("百叶") || name.Contains("卷帘") || name.Contains("晾衣") ||
            spec.Contains("curtain") || spec.Contains("airer"))
        {
            return DeviceCategoryType.Curtain;
        }

        // 5. 开关 / 插座 / 转换器 / 排插
        if (model.Contains("switch") || model.Contains("plug") || model.Contains("socket") || model.Contains("outlet") ||
            name.Contains("开关") || name.Contains("插座") || name.Contains("插头") || name.Contains("排插") || name.Contains("转换器") ||
            spec.Contains("switch") || spec.Contains("outlet"))
        {
            return DeviceCategoryType.Switch;
        }

        // 6. 环境温湿度 / 空调 / 风扇 / 空净 / 加湿 / 温控
        if (card.EnvironmentTemperature.HasValue || card.EnvironmentHumidity.HasValue ||
            (card.MetricSensors.Count > 0 && !name.Contains("灯") && !model.Contains("light")) ||
            (model.Contains("sensor") && (model.Contains("temp") || model.Contains("humid") || model.Contains("air"))) ||
            model.Contains("aircondition") || model.Contains("fan") || model.Contains("humidifier") || model.Contains("purifier") ||
            name.Contains("温湿度") || name.Contains("温度") || name.Contains("湿度") || name.Contains("空调") || name.Contains("风扇") ||
            name.Contains("加湿") || name.Contains("净化") || name.Contains("新风") || name.Contains("冷暖") ||
            spec.Contains("temperature") || spec.Contains("humidity") || spec.Contains("fan") || spec.Contains("air-purifier"))
        {
            return DeviceCategoryType.Environment;
        }

        // 7. 安防 / 门锁 / 门磁 / 人体传感器 / 监控摄像 / 烟雾报警 / 水浸
        if (model.Contains("lock") || model.Contains("magnet") || model.Contains("motion") || model.Contains("camera") ||
            model.Contains("smoke") || model.Contains("gas") || model.Contains("water") ||
            name.Contains("门锁") || name.Contains("门磁") || name.Contains("人体") || name.Contains("人在") || name.Contains("摄像") ||
            name.Contains("猫眼") || name.Contains("烟感") || name.Contains("天然气") || name.Contains("水浸") ||
            spec.Contains("lock") || spec.Contains("motion-sensor") || spec.Contains("camera"))
        {
            return DeviceCategoryType.Security;
        }

        // 8. 其他智能设备
        return DeviceCategoryType.Other;
    }

    public static string GetCategoryName(DeviceCategoryType type) => type switch
    {
        DeviceCategoryType.Lighting => "照明",
        DeviceCategoryType.LightStrip => "灯带",
        DeviceCategoryType.Speaker => "音响",
        DeviceCategoryType.Curtain => "遮阳窗帘",
        DeviceCategoryType.Switch => "开关插座",
        DeviceCategoryType.Environment => "环境温湿度",
        DeviceCategoryType.Security => "安防传感",
        _ => "其他设备"
    };

    public static string GetCategoryIcon(DeviceCategoryType type) => type switch
    {
        DeviceCategoryType.Lighting => "💡",
        DeviceCategoryType.LightStrip => "🌈",
        DeviceCategoryType.Speaker => "🔊",
        DeviceCategoryType.Curtain => "🪟",
        DeviceCategoryType.Switch => "🔌",
        DeviceCategoryType.Environment => "🌡️",
        DeviceCategoryType.Security => "🛡️",
        _ => "📱"
    };
}
