using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MiHAva.UI.ViewModels.Spec;
using MiJia.Net.Cloud;
using MiJia.Net.Models.Devices;
using MiJia.Net.Models.Spec;
using MiJia.Net.State;

namespace MiHAva.UI.ViewModels;

public sealed class SpecServiceGroupViewModel
{
    public string ServiceName { get; init; } = string.Empty;
    public int Siid { get; init; }
    public List<SpecPropertyItemViewModel> Properties { get; init; } = [];
    public List<SpecActionItemViewModel> Actions { get; init; } = [];
}

public partial class DeviceDetailViewModel : ObservableObject
{
    private readonly IMiCloudClient _client;
    private readonly IDeviceJobChannel _jobChannel;
    private readonly IDeviceStateCoordinator _coordinator;

    public DeviceInfo Device { get; }
    public MiotSpecInstance? Spec { get; }

    [ObservableProperty]
    private string _deviceName;

    [ObservableProperty]
    private string _roomName;

    [ObservableProperty]
    private bool _isOnline;

    [ObservableProperty]
    private bool _isLoading;

    // 核心大表盘数据 (参考截图 3、5、6、7)
    [ObservableProperty]
    private string _heroValue = "--";

    [ObservableProperty]
    private string _heroUnit = "";

    [ObservableProperty]
    private string _heroLabel = "设备状态";

    [ObservableProperty]
    private string _heroStatus = "正常运行";

    [ObservableProperty]
    private bool _isPowerOn;

    // 核心模式切换大分段按钮 (如：自动、睡眠、最爱、强力)
    [ObservableProperty]
    private SpecPropertyItemViewModel? _modePropertyVm;

    public ObservableCollection<ValueOption> ModeOptions { get; } = [];

    [ObservableProperty]
    private ValueOption? _currentSelectedMode;

    public ObservableCollection<SpecServiceGroupViewModel> ServiceGroups { get; } = [];

    private SpecPropertyItemViewModel? _powerPropVm;

    public DeviceDetailViewModel(
        DeviceInfo device,
        MiotSpecInstance? spec,
        IMiCloudClient client,
        IDeviceJobChannel jobChannel,
        IDeviceStateCoordinator coordinator,
        bool initialPowerState = false)
    {
        Device = device;
        Spec = spec;
        _client = client;
        _jobChannel = jobChannel;
        _coordinator = coordinator;
        _isPowerOn = initialPowerState;

        _deviceName = string.IsNullOrWhiteSpace(device.Name) ? device.Model : device.Name;
        _roomName = device.RoomName ?? "默认房间";
        _isOnline = device.IsOnline;

        BuildServiceGroups();
    }

    private void BuildServiceGroups()
    {
        if (Spec?.Services == null) return;

        ServiceGroups.Clear();
        foreach (var s in Spec.Services)
        {
            if (s.Type.Contains(":service:device-information:")) continue;

            var propVms = new List<SpecPropertyItemViewModel>();
            if (s.Properties != null)
            {
                foreach (var p in s.Properties)
                {
                    var propVm = new SpecPropertyItemViewModel(Device.Did, s.Iid, p, _jobChannel, _coordinator);
                    propVms.Add(propVm);

                    // 识别主电源开关
                    if (p.Format == "bool" && p.CanWrite && _powerPropVm == null &&
                       (p.Description.Contains("Switch", StringComparison.OrdinalIgnoreCase) ||
                        p.Description.Contains("Power", StringComparison.OrdinalIgnoreCase) ||
                        p.Description.Contains("Status", StringComparison.OrdinalIgnoreCase) ||
                        p.Type.Contains(":property:on:")))
                    {
                        _powerPropVm = propVm;
                        _powerPropVm.PropertyChanged += (sender, args) =>
                        {
                            if (args.PropertyName == nameof(SpecPropertyItemViewModel.BoolValue))
                            {
                                IsPowerOn = _powerPropVm.BoolValue;
                                RefreshHeroMetrics();
                            }
                        };
                    }

                    // 识别常用模式切换 (mode)
                    if (p.ValueList != null && p.ValueList.Count > 0 && ModePropertyVm == null &&
                       (p.Description.Contains("Mode", StringComparison.OrdinalIgnoreCase) ||
                        p.Description.Contains("模式", StringComparison.OrdinalIgnoreCase)))
                    {
                        ModePropertyVm = propVm;
                        ModeOptions.Clear();
                        foreach (var opt in propVm.Options)
                        {
                            ModeOptions.Add(opt);
                        }
                    }
                }
            }

            var actionVms = new List<SpecActionItemViewModel>();
            if (s.Actions != null)
            {
                foreach (var a in s.Actions)
                {
                    actionVms.Add(new SpecActionItemViewModel(Device.Did, s.Iid, a, _client));
                }
            }

            if (propVms.Count > 0 || actionVms.Count > 0)
            {
                ServiceGroups.Add(new SpecServiceGroupViewModel
                {
                    ServiceName = s.Description,
                    Siid = s.Iid,
                    Properties = propVms,
                    Actions = actionVms
                });
            }
        }
    }

    /// <summary>
    /// 一键切换主设备电源
    /// </summary>
    [RelayCommand]
    public async Task TogglePowerAsync()
    {
        if (_powerPropVm == null) return;
        bool target = !_powerPropVm.BoolValue;
        IsPowerOn = target;
        await _powerPropVm.SetBoolValueAsync(target);
    }

    /// <summary>
    /// 点击模式分段大按钮直接下发切换指令
    /// </summary>
    [RelayCommand]
    public async Task SelectModeAsync(ValueOption? option)
    {
        if (ModePropertyVm == null || option == null) return;
        CurrentSelectedMode = option;
        await ModePropertyVm.SetOptionAsync(option);
    }

    /// <summary>
    /// 打开窗口时拉取当前设备全部属性状态
    /// </summary>
    [RelayCommand]
    public async Task LoadPropertiesAsync()
    {
        if (!IsOnline || ServiceGroups.Count == 0) return;

        try
        {
            IsLoading = true;
            var targets = new List<MiotPropGetTarget>();

            foreach (var group in ServiceGroups)
            {
                foreach (var p in group.Properties)
                {
                    if (p.CanRead)
                    {
                        targets.Add(new MiotPropGetTarget
                        {
                            Did = Device.Did,
                            Siid = p.Siid,
                            Piid = p.Piid
                        });
                    }
                }
            }

            if (targets.Count == 0) return;

            // 分批查询
            foreach (var chunk in targets.Chunk(30))
            {
                var results = await _client.GetPropertiesAsync(chunk);
                foreach (var res in results.Where(r => r.IsSuccess))
                {
                    var propVm = ServiceGroups
                        .SelectMany(g => g.Properties)
                        .FirstOrDefault(p => p.Siid == res.Siid && p.Piid == res.Piid);

                    propVm?.UpdateRemoteValue(res.Value);
                }
            }

            // 更新 Hero 表盘数据与状态
            RefreshHeroMetrics();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[DeviceDetailVM] Fetch error: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void RefreshHeroMetrics()
    {
        if (_powerPropVm != null)
        {
            IsPowerOn = _powerPropVm.BoolValue;
        }

        if (ModePropertyVm?.SelectedOption != null)
        {
            CurrentSelectedMode = ModePropertyVm.SelectedOption;
        }

        // 智能寻找主视觉数值：如 PM2.5, 温度, 亮度, 功率等
        var allProps = ServiceGroups.SelectMany(g => g.Properties).ToList();
        var candidateHero = allProps.FirstOrDefault(p =>
            p.Description.Contains("pm2.5", StringComparison.OrdinalIgnoreCase) ||
            p.Description.Contains("温度", StringComparison.OrdinalIgnoreCase) ||
            p.Description.Contains("temperature", StringComparison.OrdinalIgnoreCase) ||
            p.Description.Contains("亮度", StringComparison.OrdinalIgnoreCase) ||
            p.Description.Contains("brightness", StringComparison.OrdinalIgnoreCase) ||
            p.Description.Contains("功率", StringComparison.OrdinalIgnoreCase) ||
            p.Description.Contains("power", StringComparison.OrdinalIgnoreCase));

        if (candidateHero != null)
        {
            HeroValue = candidateHero.DisplayText.Split(' ')[0];
            HeroUnit = candidateHero.Unit ?? "";
            HeroLabel = candidateHero.Description;
            HeroStatus = IsPowerOn ? "运行中 · 优" : "待机中";
        }
        else
        {
            HeroValue = IsPowerOn ? "开" : "关";
            HeroUnit = "";
            HeroLabel = "设备状态";
            HeroStatus = IsPowerOn ? "正常运行" : "待机中";
        }
    }
}
