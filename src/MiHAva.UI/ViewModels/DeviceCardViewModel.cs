using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MiJia.Net.Models.Devices;
using MiJia.Net.Models.Spec;
using MiJia.Net.State;

namespace MiHAva.UI.ViewModels;

public partial class DeviceCardViewModel : ObservableObject
{
    private readonly IDeviceJobChannel _jobChannel;
    private readonly IDeviceStateCoordinator _coordinator;

    public DeviceInfo Device { get; }

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

        _name = string.IsNullOrWhiteSpace(device.Name) ? device.Model : device.Name;
        _roomName = device.RoomName ?? "默认房间";
        _isOnline = device.IsOnline;
    }

    /// <summary>
    /// 初始化并绑定 Spec 规格定义
    /// </summary>
    public void ApplySpec(MiotSpecInstance? spec)
    {
        SpecInstance = spec;
        if (spec == null) return;

        // 尝试寻找开关属性 (常见为 siid:2, piid:1 或服务类型为 switch/light 的 on 属性)
        foreach (var s in spec.Services)
        {
            if (s.Properties == null) continue;
            foreach (var p in s.Properties)
            {
                if (p.Format == "bool" && p.CanWrite && 
                   (p.Description.Contains("Switch", StringComparison.OrdinalIgnoreCase) ||
                    p.Description.Contains("Status", StringComparison.OrdinalIgnoreCase) ||
                    p.Description.Contains("Power", StringComparison.OrdinalIgnoreCase) ||
                    p.Type.Contains(":property:on:")))
                {
                    SwitchSiid = s.Iid;
                    SwitchPiid = p.Iid;
                    HasSwitch = true;
                    return;
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
