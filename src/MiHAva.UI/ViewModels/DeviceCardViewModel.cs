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

    public int SwitchSiid { get; private set; } = 2;
    public int SwitchPiid { get; private set; } = 1;

    public MiotSpecInstance? SpecInstance { get; set; }

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
