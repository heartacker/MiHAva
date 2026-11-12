using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MiJia.Net.State;

namespace MiHAva.UI.ViewModels;

public enum SceneCategoryType
{
    Lighting,       // 照明 / 灯
    LightStrip,     // 灯带
    Curtain,        // 窗帘 / 遮阳
    Switch,         // 开关 / 插座
    Environment,    // 环境温湿度
    Security        // 安防
}

/// <summary>
/// 某个场景分类下，单个房间的设备分组与控制
/// </summary>
public partial class SceneCategoryRoomGroup : ObservableObject
{
    private readonly IDeviceJobChannel _jobChannel;

    public string RoomName { get; init; } = string.Empty;
    public SceneCategoryType CategoryType { get; init; }
    public ObservableCollection<DeviceCardViewModel> Devices { get; } = [];

    [ObservableProperty]
    private string _statusText = string.Empty;

    [ObservableProperty]
    private bool _hasActive;

    public bool SupportsBatchSwitch => CategoryType != SceneCategoryType.Environment && 
                                       CategoryType != SceneCategoryType.Security;

    public SceneCategoryRoomGroup(string roomName, SceneCategoryType categoryType, IDeviceJobChannel jobChannel)
    {
        RoomName = roomName;
        CategoryType = categoryType;
        _jobChannel = jobChannel;
    }

    public void RefreshStatus()
    {
        int total = Devices.Count;
        int active = Devices.Count(d => d.IsOnline && d.IsSwitchOn);
        HasActive = active > 0;

        switch (CategoryType)
        {
            case SceneCategoryType.Lighting:
            case SceneCategoryType.LightStrip:
                StatusText = active == 0 ? "灯全关着" : (active == total ? "全部点亮" : $"开启 {active} 盏");
                break;
            case SceneCategoryType.Curtain:
                StatusText = active == 0 ? "全关着" : (active == total ? "全开着" : $"开启 {active} 扇");
                break;
            case SceneCategoryType.Switch:
                StatusText = active == 0 ? "全关着" : $"开启 {active} 处";
                break;
            case SceneCategoryType.Environment:
                // 汇总本房间真实环境读数
                var envDevices = Devices.Where(d => d.EnvironmentTemperature.HasValue || d.EnvironmentHumidity.HasValue).ToList();
                if (envDevices.Count > 0)
                {
                    var temps = envDevices.Where(d => d.EnvironmentTemperature.HasValue).Select(d => d.EnvironmentTemperature!.Value).ToList();
                    var hums = envDevices.Where(d => d.EnvironmentHumidity.HasValue).Select(d => d.EnvironmentHumidity!.Value).ToList();

                    string tStr = temps.Count > 0 ? $"{temps.Average():F1}°C" : "";
                    string hStr = hums.Count > 0 ? $"{Math.Round(hums.Average()):F0}%" : "";

                    if (!string.IsNullOrEmpty(tStr) && !string.IsNullOrEmpty(hStr))
                    {
                        StatusText = $"{tStr} · {hStr}";
                    }
                    else if (!string.IsNullOrEmpty(tStr))
                    {
                        StatusText = tStr;
                    }
                    else if (!string.IsNullOrEmpty(hStr))
                    {
                        StatusText = hStr;
                    }
                    else
                    {
                        StatusText = $"{total} 件设备";
                    }
                }
                else
                {
                    var anyMetric = Devices.FirstOrDefault(d => !string.IsNullOrEmpty(d.MetricSubtitle))?.MetricSubtitle;
                    StatusText = anyMetric ?? (total > 0 ? $"{total} 件设备" : "无数据");
                }
                break;
            case SceneCategoryType.Security:
                StatusText = total > 0 ? "正常布防" : "无设备";
                break;
            default:
                StatusText = $"{active}/{total} 开启";
                break;
        }
    }

    [RelayCommand]
    public async Task TurnOnRoomAsync()
    {
        var targets = Devices.Where(d => d.IsOnline && d.HasSwitch && !d.IsSwitchOn).ToList();
        foreach (var dev in targets)
        {
            dev.IsSwitchOn = true;
            await _jobChannel.EnqueueSetPropertyAsync(dev.Device.Did, dev.SwitchSiid, dev.SwitchPiid, true);
        }
        RefreshStatus();
    }

    [RelayCommand]
    public async Task TurnOffRoomAsync()
    {
        var targets = Devices.Where(d => d.IsOnline && d.HasSwitch && d.IsSwitchOn).ToList();
        foreach (var dev in targets)
        {
            dev.IsSwitchOn = false;
            await _jobChannel.EnqueueSetPropertyAsync(dev.Device.Did, dev.SwitchSiid, dev.SwitchPiid, false);
        }
        RefreshStatus();
    }
}

/// <summary>
/// 全屋某一大类的汇总聚合项（对应截图中的“照明 >”、“遮阳 >”等板块）
/// </summary>
public partial class SceneCategorySummaryGroup : ObservableObject
{
    private readonly IDeviceJobChannel _jobChannel;

    public SceneCategoryType CategoryType { get; init; }
    public string Title { get; init; } = string.Empty;
    public string Icon { get; init; } = string.Empty;

    [ObservableProperty]
    private string _statusText = string.Empty;

    [ObservableProperty]
    private int _totalDeviceCount;

    [ObservableProperty]
    private int _activeDeviceCount;

    [ObservableProperty]
    private bool _hasActive;

    public bool SupportsBatchSwitch => CategoryType != SceneCategoryType.Environment && 
                                       CategoryType != SceneCategoryType.Security;

    public ObservableCollection<SceneCategoryRoomGroup> RoomGroups { get; } = [];

    public event Action<SceneCategorySummaryGroup>? OpenDetailRequested;

    public SceneCategorySummaryGroup(
        SceneCategoryType categoryType,
        string title,
        string icon,
        IDeviceJobChannel jobChannel)
    {
        CategoryType = categoryType;
        Title = title;
        Icon = icon;
        _jobChannel = jobChannel;
    }

    public void Rebuild(IEnumerable<DeviceCardViewModel> allDevices)
    {
        var matched = allDevices.Where(MatchesCategory).ToList();
        TotalDeviceCount = matched.Count;
        ActiveDeviceCount = matched.Count(d => d.IsOnline && d.IsSwitchOn);
        HasActive = ActiveDeviceCount > 0;

        switch (CategoryType)
        {
            case SceneCategoryType.Lighting:
                StatusText = ActiveDeviceCount == 0 ? "灯全关着" : $"全屋 {ActiveDeviceCount} 盏开启";
                break;
            case SceneCategoryType.LightStrip:
                StatusText = ActiveDeviceCount == 0 ? "灯带全关着" : $"全屋 {ActiveDeviceCount} 条开启";
                break;
            case SceneCategoryType.Curtain:
                StatusText = ActiveDeviceCount == 0 ? "全屋窗帘关着" : "全屋窗帘开着";
                break;
            case SceneCategoryType.Switch:
                StatusText = ActiveDeviceCount == 0 ? "开关全关着" : $"{ActiveDeviceCount} 处开关开启";
                break;
            case SceneCategoryType.Environment:
                StatusText = $"{TotalDeviceCount} 个环境传感器 · 室内舒适";
                break;
            case SceneCategoryType.Security:
                StatusText = $"{TotalDeviceCount} 个安防设备 · 正常守护中";
                break;
        }

        RoomGroups.Clear();
        var byRoom = matched.GroupBy(d => d.RoomName);
        foreach (var g in byRoom)
        {
            var roomGroup = new SceneCategoryRoomGroup(g.Key, CategoryType, _jobChannel);
            foreach (var d in g)
            {
                roomGroup.Devices.Add(d);
            }
            roomGroup.RefreshStatus();
            RoomGroups.Add(roomGroup);
        }
    }

    private bool MatchesCategory(DeviceCardViewModel card)
    {
        string m = card.Device.Model.ToLowerInvariant();
        string n = card.Name.ToLowerInvariant();

        return CategoryType switch
        {
            SceneCategoryType.Lighting => 
                (m.Contains("light") || m.Contains("lamp") || n.Contains("灯") || n.Contains("筒灯") || n.Contains("吸顶灯")) &&
                !m.Contains("strip") && !n.Contains("灯带") && !n.Contains("氛围灯"),

            SceneCategoryType.LightStrip => 
                m.Contains("strip") || n.Contains("灯带") || n.Contains("氛围灯") || n.Contains("光带"),

            SceneCategoryType.Curtain => 
                m.Contains("curtain") || m.Contains("shade") || m.Contains("blind") || n.Contains("窗帘") || n.Contains("百叶") || n.Contains("卷帘"),

            SceneCategoryType.Switch => 
                m.Contains("switch") || m.Contains("plug") || m.Contains("socket") || n.Contains("开关") || n.Contains("插座") || n.Contains("插头"),

            SceneCategoryType.Environment => 
                card.EnvironmentTemperature.HasValue || card.EnvironmentHumidity.HasValue ||
                (card.MetricSensors.Count > 0 && !m.Contains("light") && !m.Contains("lamp") && !n.Contains("灯")) ||
                (m.Contains("sensor") && !m.Contains("motion") && !m.Contains("magnet")) ||
                m.Contains("temp") || m.Contains("humid") || m.Contains("air") || m.Contains("fan") ||
                n.Contains("温湿度") || n.Contains("温度") || n.Contains("湿度") || n.Contains("空调") || n.Contains("加湿") || n.Contains("净化") || n.Contains("风扇"),

            SceneCategoryType.Security => 
                m.Contains("lock") || m.Contains("magnet") || m.Contains("motion") || m.Contains("camera") || m.Contains("smoke") || m.Contains("gas") ||
                n.Contains("门锁") || n.Contains("门磁") || n.Contains("人体") || n.Contains("摄像") || n.Contains("烟感") || n.Contains("水浸") || n.Contains("天然气"),

            _ => false
        };
    }

    [RelayCommand]
    public void OpenDetail()
    {
        OpenDetailRequested?.Invoke(this);
    }

    [RelayCommand]
    public async Task TurnOffAllAsync()
    {
        foreach (var room in RoomGroups)
        {
            await room.TurnOffRoomAsync();
        }
        TotalDeviceCount = RoomGroups.Sum(r => r.Devices.Count);
        ActiveDeviceCount = RoomGroups.Sum(r => r.Devices.Count(d => d.IsOnline && d.IsSwitchOn));
        HasActive = ActiveDeviceCount > 0;
        StatusText = ActiveDeviceCount == 0 ? "全部已关闭" : $"剩余 {ActiveDeviceCount} 件开启";
    }

    [RelayCommand]
    public async Task TurnOnAllAsync()
    {
        foreach (var room in RoomGroups)
        {
            await room.TurnOnRoomAsync();
        }
        TotalDeviceCount = RoomGroups.Sum(r => r.Devices.Count);
        ActiveDeviceCount = RoomGroups.Sum(r => r.Devices.Count(d => d.IsOnline && d.IsSwitchOn));
        HasActive = ActiveDeviceCount > 0;
        StatusText = $"全屋 {ActiveDeviceCount} 件已开启";
    }
}
