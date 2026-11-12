using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MiHAva.UI.Services;
using MiJia.Net.Models.Devices;
using MiJia.Net.Models.Spec;
using MiJia.Net.State;

namespace MiHAva.UI.ViewModels;

public partial class DeviceCardViewModel : ObservableObject
{
    private readonly IDeviceJobChannel _jobChannel;
    private readonly IDeviceStateCoordinator _coordinator;

    public DeviceInfo Device { get; }
    public string OriginalCloudName { get; }

    [ObservableProperty]
    private string _name;

    [ObservableProperty]
    private string _roomName;

    [ObservableProperty]
    private bool _isOnline;

    [ObservableProperty]
    private bool _hasSwitch;

    [ObservableProperty]
    private bool _isSwitchOn;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string _metricSubtitle = string.Empty;

    public double? EnvironmentTemperature { get; private set; }
    public double? EnvironmentHumidity { get; private set; }

    public string StatusSummaryText
    {
        get
        {
            if (!IsOnline) return "离线";
            if (!HasSwitch)
            {
                return !string.IsNullOrEmpty(MetricSubtitle) ? MetricSubtitle : "在线";
            }
            string switchText = IsSwitchOn ? "已开启" : "已关闭";
            return !string.IsNullOrEmpty(MetricSubtitle) ? $"{switchText} · {MetricSubtitle}" : switchText;
        }
    }

    public bool IsActiveStatus => IsOnline && HasSwitch && IsSwitchOn;
    public bool IsOfflineStatus => !IsOnline;

    partial void OnIsOnlineChanged(bool value)
    {
        OnPropertyChanged(nameof(StatusSummaryText));
        OnPropertyChanged(nameof(IsOfflineStatus));
        OnPropertyChanged(nameof(IsActiveStatus));
    }

    partial void OnIsSwitchOnChanged(bool value)
    {
        OnPropertyChanged(nameof(StatusSummaryText));
        OnPropertyChanged(nameof(IsActiveStatus));
    }

    partial void OnMetricSubtitleChanged(string value)
    {
        OnPropertyChanged(nameof(StatusSummaryText));
    }

    public List<(int siid, int piid, string desc, string? unit)> MetricSensors { get; } = [];
    private readonly Dictionary<(int siid, int piid), string> _metricValues = [];

    /// <summary>
    /// 设备品类图标 Emoji / 图标
    /// </summary>
    public string CategoryIcon => ResolveCategoryIcon(Device.Model, Name);

    public int SwitchSiid { get; private set; } = 2;
    public int SwitchPiid { get; private set; } = 1;

    public MiotSpecInstance? SpecInstance { get; set; }

    private static string ResolveCategoryIcon(string model, string name)
    {
        string m = model.ToLowerInvariant();
        string n = name.ToLowerInvariant();

        if (m.Contains("light") || m.Contains("lamp") || n.Contains("灯")) return "💡";
        if (m.Contains("switch") || n.Contains("开关")) return "🔘";
        if (m.Contains("plug") || m.Contains("socket") || n.Contains("插座") || n.Contains("插头")) return "🔌";
        if (m.Contains("gateway") || n.Contains("网关")) return "📡";
        if (m.Contains("curtain") || n.Contains("窗帘")) return "🪟";
        if (m.Contains("aircondition") || m.Contains("airer") || n.Contains("空调")) return "❄️";
        if (m.Contains("fan") || n.Contains("风扇")) return "🌀";
        if (m.Contains("airpurifier") || n.Contains("净化器")) return "🍃";
        if (m.Contains("vacuum") || n.Contains("扫地")) return "🧹";
        if (m.Contains("camera") || n.Contains("摄像")) return "📷";
        if (m.Contains("speaker") || m.Contains("audio") || n.Contains("音箱") || n.Contains("小爱")) return "🔊";
        if (m.Contains("sensor") || m.Contains("magnet") || n.Contains("传感器") || n.Contains("门磁") || n.Contains("人体")) return "🚨";
        if (m.Contains("watch") || m.Contains("band") || n.Contains("手环") || n.Contains("手表")) return "⌚";
        if (m.Contains("lock") || n.Contains("门锁")) return "🔒";
        if (m.Contains("humidifier") || n.Contains("加湿")) return "💧";
        if (m.Contains("heater") || n.Contains("取暖") || n.Contains("暖气")) return "🔥";
        if (m.Contains("tv") || n.Contains("电视")) return "📺";

        return "📱";
    }

    /// <summary>
    /// 请求打开设备详情弹窗事件
    /// </summary>
    public event Action<DeviceCardViewModel>? OpenDetailRequested;

    [RelayCommand]
    public void OpenDetail()
    {
        OpenDetailRequested?.Invoke(this);
    }

    public DeviceCardViewModel(
        DeviceInfo device,
        IDeviceJobChannel jobChannel,
        IDeviceStateCoordinator coordinator)
    {
        Device = device;
        _jobChannel = jobChannel;
        _coordinator = coordinator;

        OriginalCloudName = string.IsNullOrWhiteSpace(device.Name) ? device.Model : device.Name;
        _name = DeviceSettingsService.GetCustomName(device.Did) ?? OriginalCloudName;
        _roomName = device.RoomName ?? "默认房间";
        _isOnline = device.IsOnline;
    }

    public void UpdateName(string newName)
    {
        Name = newName;
    }

    /// <summary>
    /// 初始化并绑定 Spec 规格定义
    /// </summary>
    public void ApplySpec(MiotSpecInstance? spec)
    {
        SpecInstance = spec;
        if (spec == null) return;

        MetricSensors.Clear();

        // 尝试寻找开关属性与常用环境/功率指标
        foreach (var s in spec.Services)
        {
            if (s.Properties == null) continue;
            foreach (var p in s.Properties)
            {
                if (!HasSwitch && p.Format == "bool" && p.CanWrite && 
                   (p.Description.Contains("Switch", StringComparison.OrdinalIgnoreCase) ||
                    p.Description.Contains("Status", StringComparison.OrdinalIgnoreCase) ||
                    p.Description.Contains("Power", StringComparison.OrdinalIgnoreCase) ||
                    p.Type.Contains(":property:on:")))
                {
                    SwitchSiid = s.Iid;
                    SwitchPiid = p.Iid;
                    HasSwitch = true;
                    OnPropertyChanged(nameof(StatusSummaryText));
                    OnPropertyChanged(nameof(IsActiveStatus));
                }
                else if (p.CanRead && 
                        (MetricFormatter.IsEnvironmentTemperature(p.Description) ||
                         MetricFormatter.IsHumidity(p.Description) ||
                         p.Description.Contains("power", StringComparison.OrdinalIgnoreCase) ||
                         p.Description.Contains("pm2.5", StringComparison.OrdinalIgnoreCase)))
                {
                    MetricSensors.Add((s.Iid, p.Iid, p.Description, p.Unit));
                }
            }
        }
    }

    /// <summary>
    /// 更新最新属性状态（配合防回弹租约）
    /// </summary>
    public void UpdateProperty(int siid, int piid, object? value)
    {
        if (siid == SwitchSiid && piid == SwitchPiid)
        {
            if (_coordinator.ShouldAcceptRemoteValue(Device.Did, siid, piid, value, out var effective))
            {
                if (effective is bool b)
                {
                    IsSwitchOn = b;
                }
                else if (effective is System.Text.Json.JsonElement je)
                {
                    if (je.ValueKind == System.Text.Json.JsonValueKind.True) IsSwitchOn = true;
                    else if (je.ValueKind == System.Text.Json.JsonValueKind.False) IsSwitchOn = false;
                }
            }
        }
        else
        {
            // 检查是否为副标题传感器指标
            var sensor = MetricSensors.FirstOrDefault(m => m.siid == siid && m.piid == piid);
            if (sensor != default && value != null)
            {
                if (double.TryParse(value.ToString(), out double num))
                {
                    if (MetricFormatter.IsEnvironmentTemperature(sensor.desc))
                    {
                        double realT = num;
                        if (Math.Abs(realT) > 60 && Math.Abs(realT) < 1000) realT /= 10.0;
                        else if (Math.Abs(realT) >= 1000) realT /= 100.0;
                        if (realT >= -30 && realT <= 60)
                        {
                            EnvironmentTemperature = realT;
                        }
                    }
                    else if (MetricFormatter.IsHumidity(sensor.desc))
                    {
                        double realH = num;
                        if (realH > 100 && realH < 1000) realH /= 10.0;
                        else if (realH >= 1000) realH /= 100.0;
                        if (realH >= 0 && realH <= 100)
                        {
                            EnvironmentHumidity = realH;
                        }
                    }

                    string formatted = MetricFormatter.FormatMetric(sensor.desc, num, sensor.unit);
                    _metricValues[(siid, piid)] = formatted;
                    MetricSubtitle = string.Join(" · ", _metricValues.Values);
                }
            }
        }
    }

    /// <summary>
    /// 点击界面 Toggle 开关切换状态
    /// </summary>
    [RelayCommand]
    private async Task ToggleSwitchAsync()
    {
        if (!IsOnline || !HasSwitch || IsBusy) return;

        bool targetState = !IsSwitchOn;
        // 本地立即更新呈现（提升响应感）
        IsSwitchOn = targetState;

        try
        {
            IsBusy = true;
            await _jobChannel.EnqueueSetPropertyAsync(Device.Did, SwitchSiid, SwitchPiid, targetState);
        }
        finally
        {
            IsBusy = false;
        }
    }
}
