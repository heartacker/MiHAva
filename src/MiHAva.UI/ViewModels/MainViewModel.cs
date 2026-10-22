using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MiHAva.UI.Services;
using MiJia.Net.Cloud;
using MiJia.Net.Models.Auth;
using MiJia.Net.Models.Devices;
using MiJia.Net.Spec;
using MiJia.Net.State;

namespace MiHAva.UI.ViewModels;

public sealed class RoomCategoryItem
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public int DeviceCount { get; set; }

    public override string ToString() => $"{Name} ({DeviceCount})";
}

public partial class MainViewModel : ObservableObject, IAsyncDisposable
{
    private readonly IMiCloudClient _client;
    private readonly IMiotSpecClient _specClient;
    private readonly IDeviceJobChannel _jobChannel;
    private readonly IDeviceStateCoordinator _coordinator;

    private readonly List<DeviceCardViewModel> _allDeviceCards = [];
    private CancellationTokenSource? _pollLoopCts;

    [ObservableProperty]
    private string _currentHomeName = "我的家";

    [ObservableProperty]
    private string _searchKeyword = string.Empty;

    [ObservableProperty]
    private bool _isRefreshing;

    [ObservableProperty]
    private RoomCategoryItem? _selectedRoom;

    [ObservableProperty]
    private int _totalDeviceCount;

    [ObservableProperty]
    private int _activeDeviceCount;

    [ObservableProperty]
    private string _summaryText = "加载中...";

    public ObservableCollection<RoomCategoryItem> Rooms { get; } = [];
    public ObservableCollection<DeviceCardViewModel> FilteredDevices { get; } = [];
    public XiaoAiCommandViewModel XiaoAiVm { get; }

    /// <summary>
    /// 一键关灯快捷场景
    /// </summary>
    [RelayCommand]
    public async Task TurnOffAllLightsAsync()
    {
        var lightCards = _allDeviceCards
            .Where(c => c.IsOnline && c.HasSwitch && c.IsSwitchOn &&
                       (c.Name.Contains("灯") || c.Device.Model.Contains("light") || c.Device.Model.Contains("lamp")))
            .ToList();

        foreach (var card in lightCards)
        {
            card.IsSwitchOn = false;
            await _jobChannel.EnqueueSetPropertyAsync(card.Device.Did, card.SwitchSiid, card.SwitchPiid, false);
        }
        UpdateSummary();
    }

    /// <summary>
    /// 离家模式快捷场景
    /// </summary>
    [RelayCommand]
    public async Task LeaveHomeSceneAsync()
    {
        // 离家：关闭所有非必须设备
        var activeCards = _allDeviceCards
            .Where(c => c.IsOnline && c.HasSwitch && c.IsSwitchOn &&
                       !c.Name.Contains("路由") && !c.Name.Contains("网关") && !c.Name.Contains("摄像"))
            .ToList();

        foreach (var card in activeCards)
        {
            card.IsSwitchOn = false;
            await _jobChannel.EnqueueSetPropertyAsync(card.Device.Did, card.SwitchSiid, card.SwitchPiid, false);
        }
        UpdateSummary();
    }

    /// <summary>
    /// 回家模式快捷场景 (打开常用客厅灯与净化器等)
    /// </summary>
    [RelayCommand]
    public async Task ArriveHomeSceneAsync()
    {
        var welcomeCards = _allDeviceCards
            .Where(c => c.IsOnline && c.HasSwitch && !c.IsSwitchOn &&
                       (c.Name.Contains("灯") || c.Name.Contains("净化器") || c.RoomName.Contains("客厅")))
            .ToList();

        foreach (var card in welcomeCards)
        {
            card.IsSwitchOn = true;
            await _jobChannel.EnqueueSetPropertyAsync(card.Device.Did, card.SwitchSiid, card.SwitchPiid, true);
        }
        UpdateSummary();
    }

    /// <summary>
    /// 更新全屋设备统计与概览文案
    /// </summary>
    public void UpdateSummary()
    {
        TotalDeviceCount = _allDeviceCards.Count;
        ActiveDeviceCount = _allDeviceCards.Count(c => c.IsOnline && c.IsSwitchOn);
        SummaryText = $"全屋 {TotalDeviceCount} 台设备 · {ActiveDeviceCount} 台运行中";
    }

    /// <summary>
    /// 请求弹出设备详情对话框
    /// </summary>
    public event Action<DeviceDetailViewModel>? OpenDeviceDetailRequested;

    /// <summary>
    /// 请求切换桌面挂件显示
    /// </summary>
    public event Action? ToggleDesktopBoardRequested;

    [RelayCommand]
    public void ToggleDesktopBoard()
    {
        ToggleDesktopBoardRequested?.Invoke();
    }

    public MainViewModel(
        IMiCloudClient client,
        IMiotSpecClient specClient,
        IDeviceJobChannel jobChannel,
        IDeviceStateCoordinator coordinator)
    {
        _client = client;
        _specClient = specClient;
        _jobChannel = jobChannel;
        _coordinator = coordinator;
        XiaoAiVm = new XiaoAiCommandViewModel(client);
    }

    [ObservableProperty]
    private string _currentThemeIcon = "🌙";

    /// <summary>
    /// 切换浅色 / 深色主题
    /// </summary>
    [RelayCommand]
    public void ToggleTheme()
    {
        if (Avalonia.Application.Current != null)
        {
            var currentVariant = Avalonia.Application.Current.ActualThemeVariant;
            if (currentVariant == Avalonia.Styling.ThemeVariant.Dark)
            {
                Avalonia.Application.Current.RequestedThemeVariant = Avalonia.Styling.ThemeVariant.Light;
                CurrentThemeIcon = "🌙"; // 提示可切到暗色
            }
            else
            {
                Avalonia.Application.Current.RequestedThemeVariant = Avalonia.Styling.ThemeVariant.Dark;
                CurrentThemeIcon = "☀️"; // 提示可切到浅色
            }
        }
    }

    /// <summary>
    /// 初始化加载全部数据 (Offline-First 秒开呈现)
    /// </summary>
    public async Task InitializeAsync()
    {
        // 1. 优先读取本地磁盘缓存，实现 UI 0 毫秒秒开
        var cached = await DeviceCacheService.LoadCacheAsync();
        if (cached != null && cached.Devices.Count > 0)
        {
            ApplyHomeAndDeviceData(cached.Homes, cached.Devices);
        }

        // 2. 静默拉取最新数据
        _ = RefreshAllAsync();
        StartBackgroundPolling();
    }

    /// <summary>
    /// 手动刷新
    /// </summary>
    [RelayCommand]
    public async Task RefreshAllAsync()
    {
        if (IsRefreshing) return;
        IsRefreshing = true;

        try
        {
            // 1. 获取家庭与房间
            var homes = await _client.GetHomesAsync();

            // 2. 获取设备列表
            var rawDevices = await _client.GetDevicesAsync();

            // 3. 应用数据并刷新界面
            ApplyHomeAndDeviceData(homes, rawDevices);

            // 4. 异步持久化到磁盘缓存
            _ = DeviceCacheService.SaveCacheAsync(homes, rawDevices);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MainVM] Refresh error: {ex.Message}");
        }
        finally
        {
            IsRefreshing = false;
        }
    }

    private void ApplyHomeAndDeviceData(List<HomeInfo> homes, List<DeviceInfo> rawDevices)
    {
        var primaryHome = homes.FirstOrDefault();
        if (primaryHome != null)
        {
            CurrentHomeName = primaryHome.Name;
        }

        // 建立 did -> roomName 映射字典
        var didToRoomName = new Dictionary<string, string>();
        var roomCategories = new List<RoomCategoryItem>
        {
            new() { Id = "__ALL__", Name = "全部设备", DeviceCount = rawDevices.Count }
        };

        if (primaryHome != null)
        {
            foreach (var r in primaryHome.Rooms)
            {
                foreach (var did in r.Dids)
                {
                    didToRoomName[did] = r.Name;
                }
                roomCategories.Add(new RoomCategoryItem
                {
                    Id = r.Id,
                    Name = r.Name,
                    DeviceCount = r.Dids.Count
                });
            }
        }

        // 更新房间列表
        Rooms.Clear();
        foreach (var r in roomCategories)
        {
            Rooms.Add(r);
        }
        SelectedRoom = Rooms.FirstOrDefault();

        // 自动刷新可用的小爱音箱
        XiaoAiVm.RefreshSpeakers(rawDevices);

        // 3. 构建卡片并加载 Spec
        _allDeviceCards.Clear();
        foreach (var dev in rawDevices)
        {
            if (didToRoomName.TryGetValue(dev.Did, out var rName))
            {
                dev.RoomName = rName;
            }
            var card = new DeviceCardViewModel(dev, _jobChannel, _coordinator);
            card.OpenDetailRequested += OnCardOpenDetailRequested;
            _allDeviceCards.Add(card);
        }

        // 异步解析各设备的 Spec
        _ = Task.Run(async () =>
        {
            foreach (var card in _allDeviceCards)
            {
                if (!string.IsNullOrEmpty(card.Device.SpecType))
                {
                    var spec = await _specClient.GetSpecInstanceAsync(card.Device.SpecType);
                    card.ApplySpec(spec);
                }
            }
            // 拉取一轮开关状态
            await PollDevicePropertiesOnceAsync();
        });

        ApplyFilter();
        UpdateSummary();
    }

    partial void OnSearchKeywordChanged(string value) => ApplyFilter();
    partial void OnSelectedRoomChanged(RoomCategoryItem? value) => ApplyFilter();

    private void ApplyFilter()
    {
        var query = _allDeviceCards.AsEnumerable();

        // 房间过滤
        if (SelectedRoom != null && SelectedRoom.Id != "__ALL__")
        {
            query = query.Where(d => d.RoomName == SelectedRoom.Name);
        }

        // 关键词过滤
        if (!string.IsNullOrWhiteSpace(SearchKeyword))
        {
            string kw = SearchKeyword.Trim();
            query = query.Where(d =>
                d.Name.Contains(kw, StringComparison.OrdinalIgnoreCase) ||
                d.Device.Model.Contains(kw, StringComparison.OrdinalIgnoreCase) ||
                d.RoomName.Contains(kw, StringComparison.OrdinalIgnoreCase));
        }

        FilteredDevices.Clear();
        foreach (var item in query)
        {
            FilteredDevices.Add(item);
        }
    }

    /// <summary>
    /// 后台周期性轮询属性状态
    /// </summary>
    private void StartBackgroundPolling()
    {
        _pollLoopCts?.Cancel();
        _pollLoopCts = new CancellationTokenSource();

        _ = Task.Run(async () =>
        {
            using var periodicTimer = new PeriodicTimer(TimeSpan.FromSeconds(8));
            while (!_pollLoopCts.Token.IsCancellationRequested &&
                   await periodicTimer.WaitForNextTickAsync(_pollLoopCts.Token))
            {
                try
                {
                    await PollDevicePropertiesOnceAsync();
                }
                catch
                {
                    // 忽略单次网络闪断
                }
            }
        });
    }

    private async Task PollDevicePropertiesOnceAsync()
    {
        // 收集所有具有开关的卡片
        var switchTargets = _allDeviceCards
            .Where(c => c.IsOnline && c.HasSwitch)
            .Select(c => new MiotPropGetTarget
            {
                Did = c.Device.Did,
                Siid = c.SwitchSiid,
                Piid = c.SwitchPiid
            })
            .ToList();

        if (switchTargets.Count == 0) return;

        // 分批查询（每批最多 30 个，避免单包超限）
        foreach (var batch in switchTargets.Chunk(30))
        {
            var results = await _client.GetPropertiesAsync(batch);
            foreach (var r in results.Where(x => x.IsSuccess))
            {
                var card = _allDeviceCards.FirstOrDefault(c => c.Device.Did == r.Did);
                card?.UpdateProperty(r.Siid, r.Piid, r.Value);
            }
        }
    }

    private void OnCardOpenDetailRequested(DeviceCardViewModel card)
    {
        var detailVm = new DeviceDetailViewModel(
            card.Device,
            card.SpecInstance,
            _client,
            _jobChannel,
            _coordinator);

        OpenDeviceDetailRequested?.Invoke(detailVm);
    }

    public async ValueTask DisposeAsync()
    {
        _pollLoopCts?.Cancel();
        _pollLoopCts?.Dispose();
        await _jobChannel.DisposeAsync();
    }
}
