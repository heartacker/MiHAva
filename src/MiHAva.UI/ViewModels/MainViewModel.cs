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
    public int ActiveCount { get; set; }

    public override string ToString() => $"{Name} ({DeviceCount})";
}

public sealed class HomeCategoryItem
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public int RoomCount { get; init; }
    public int DeviceCount { get; set; }
    public HomeInfo RawHome { get; init; } = default!;

    public override string ToString() => Name;
}

/// <summary>
/// 按房间分组的设备区块（米家经典“全部”视图分段展示）
/// </summary>
public sealed class RoomSectionGroup
{
    public string RoomName { get; init; } = string.Empty;
    public int DeviceCount => Devices.Count;
    public int ActiveCount => Devices.Count(d => d.IsOnline && d.IsSwitchOn);
    public string SummaryText => $"{DeviceCount} 台设备 · {ActiveCount} 台开启";
    public ObservableCollection<DeviceCardViewModel> Devices { get; } = [];
}

public partial class MainViewModel : ObservableObject, IAsyncDisposable
{
    private readonly IMiCloudClient _client;
    private readonly IMiotSpecClient _specClient;
    private readonly IDeviceJobChannel _jobChannel;
    private readonly IDeviceStateCoordinator _coordinator;

    private readonly List<DeviceCardViewModel> _allDeviceCards = [];
    private readonly List<DeviceInfo> _allRawDevices = [];
    private CancellationTokenSource? _pollLoopCts;

    [ObservableProperty]
    private HomeCategoryItem? _selectedHome;

    [ObservableProperty]
    private string _currentHomeName = "我的家";

    [ObservableProperty]
    private string _environmentSummary = "室内舒适 · 24°C";

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

    [ObservableProperty]
    private string _currentTabTitle = "全部设备";

    [ObservableProperty]
    private string _currentTabSubtitle = "全屋设备一览";

    [ObservableProperty]
    private bool _isAllRoomSelected = true;

    [ObservableProperty]
    private bool _isHomeSwitcherOpen;

    public ObservableCollection<HomeCategoryItem> AvailableHomes { get; } = [];
    public ObservableCollection<RoomCategoryItem> Rooms { get; } = [];
    public ObservableCollection<DeviceCardViewModel> FilteredDevices { get; } = [];
    public ObservableCollection<RoomSectionGroup> RoomSections { get; } = [];
    public XiaoAiCommandViewModel XiaoAiVm { get; }

    [RelayCommand]
    public void ToggleHomeSwitcher()
    {
        IsHomeSwitcherOpen = !IsHomeSwitcherOpen;
    }

    [RelayCommand]
    public void SelectHome(HomeCategoryItem home)
    {
        SelectedHome = home;
        CurrentHomeName = home.Name;
        IsHomeSwitcherOpen = false;
        RebuildRoomsForCurrentHome();
    }

    [RelayCommand]
    public void SwitchToRoom(RoomCategoryItem room)
    {
        SelectedRoom = room;
    }

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
    /// 回家模式快捷场景
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
    /// 全开模式快捷场景
    /// </summary>
    [RelayCommand]
    public async Task TurnOnAllSceneAsync()
    {
        var offCards = _allDeviceCards
            .Where(c => c.IsOnline && c.HasSwitch && !c.IsSwitchOn &&
                       (c.Name.Contains("灯") || c.Device.Model.Contains("light")))
            .ToList();

        foreach (var card in offCards)
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
        SummaryText = $"{TotalDeviceCount} 台设备 · {ActiveDeviceCount} 台运行中";

        // 更新每个 Room 的 ActiveCount
        foreach (var r in Rooms)
        {
            if (r.Id == "__ALL__")
            {
                r.DeviceCount = TotalDeviceCount;
                r.ActiveCount = ActiveDeviceCount;
            }
            else
            {
                var roomCards = _allDeviceCards.Where(c => c.RoomName == r.Name).ToList();
                r.DeviceCount = roomCards.Count;
                r.ActiveCount = roomCards.Count(c => c.IsOnline && c.IsSwitchOn);
            }
        }

        UpdateCurrentTabInfo();
    }

    private void UpdateCurrentTabInfo()
    {
        if (SelectedRoom == null || SelectedRoom.Id == "__ALL__")
        {
            IsAllRoomSelected = true;
            CurrentTabTitle = "全部设备";
            CurrentTabSubtitle = $"全屋 {TotalDeviceCount} 台智能设备 · {ActiveDeviceCount} 台正在运行";
        }
        else
        {
            IsAllRoomSelected = false;
            CurrentTabTitle = SelectedRoom.Name;
            var inRoom = _allDeviceCards.Where(c => c.RoomName == SelectedRoom.Name).ToList();
            int inRoomActive = inRoom.Count(c => c.IsOnline && c.IsSwitchOn);
            CurrentTabSubtitle = $"{SelectedRoom.Name} 专属空间 · 共 {inRoom.Count} 台设备 · {inRoomActive} 台已开启";
        }
    }

    public event Action<DeviceDetailViewModel>? OpenDeviceDetailRequested;
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

    [RelayCommand]
    public void ToggleTheme()
    {
        if (Avalonia.Application.Current != null)
        {
            var currentVariant = Avalonia.Application.Current.ActualThemeVariant;
            if (currentVariant == Avalonia.Styling.ThemeVariant.Dark)
            {
                Avalonia.Application.Current.RequestedThemeVariant = Avalonia.Styling.ThemeVariant.Light;
                CurrentThemeIcon = "🌙";
            }
            else
            {
                Avalonia.Application.Current.RequestedThemeVariant = Avalonia.Styling.ThemeVariant.Dark;
                CurrentThemeIcon = "☀️";
            }
        }
    }

    public async Task InitializeAsync()
    {
        var cached = await DeviceCacheService.LoadCacheAsync();
        if (cached != null && cached.Devices.Count > 0)
        {
            ApplyHomeAndDeviceData(cached.Homes, cached.Devices);
        }

        _ = RefreshAllAsync();
        StartBackgroundPolling();
    }

    [RelayCommand]
    public async Task RefreshAllAsync()
    {
        if (IsRefreshing) return;
        IsRefreshing = true;

        try
        {
            var homes = await _client.GetHomesAsync();
            var rawDevices = await _client.GetDevicesAsync();

            ApplyHomeAndDeviceData(homes, rawDevices);
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
        _allRawDevices.Clear();
        _allRawDevices.AddRange(rawDevices);

        AvailableHomes.Clear();
        foreach (var h in homes)
        {
            // 统计该家庭匹配到的设备数量
            var homeDids = new HashSet<string>();
            foreach (var r in h.Rooms)
            {
                foreach (var rdid in r.Dids)
                {
                    homeDids.Add(rdid);
                    int dotIdx = rdid.IndexOf('.');
                    if (dotIdx > 0) homeDids.Add(rdid[..dotIdx]);
                }
            }
            int count = rawDevices.Count(d => homeDids.Contains(d.Did));

            AvailableHomes.Add(new HomeCategoryItem
            {
                Id = h.Id,
                Name = h.Name,
                RoomCount = h.Rooms.Count,
                DeviceCount = count,
                RawHome = h
            });
        }

        if (SelectedHome == null || !AvailableHomes.Any(h => h.Id == SelectedHome.Id))
        {
            SelectedHome = AvailableHomes.FirstOrDefault();
        }

        if (SelectedHome != null)
        {
            CurrentHomeName = SelectedHome.Name;
        }

        RebuildRoomsForCurrentHome();
    }

    private void RebuildRoomsForCurrentHome()
    {
        var rawHome = SelectedHome?.RawHome;
        var didToRoomName = new Dictionary<string, string>();
        var homeDids = new HashSet<string>();

        var roomCategories = new List<RoomCategoryItem>
        {
            new() { Id = "__ALL__", Name = "全部", DeviceCount = 0 }
        };

        if (rawHome != null)
        {
            foreach (var r in rawHome.Rooms)
            {
                foreach (var rdid in r.Dids)
                {
                    didToRoomName[rdid] = r.Name;
                    homeDids.Add(rdid);

                    // 兼容多键开关分拆 did (例如 1122187271.s2 -> 主设备 1122187271)
                    int dotIdx = rdid.IndexOf('.');
                    if (dotIdx > 0)
                    {
                        string baseDid = rdid[..dotIdx];
                        didToRoomName[baseDid] = r.Name;
                        homeDids.Add(baseDid);
                    }
                }

                roomCategories.Add(new RoomCategoryItem
                {
                    Id = r.Id,
                    Name = r.Name,
                    DeviceCount = r.Dids.Count
                });
            }
        }

        // 筛选归属于当前家庭的设备
        var currentHomeDevices = _allRawDevices.Where(d => homeDids.Count == 0 || homeDids.Contains(d.Did)).ToList();
        
        // 若为主家庭，将未分配房间的穿戴设备（如手环等）也收入“全部”
        if (SelectedHome == AvailableHomes.FirstOrDefault())
        {
            var unassigned = _allRawDevices.Where(d => !AvailableHomes.Skip(1).Any(other =>
            {
                var otherDids = other.RawHome.Rooms.SelectMany(r => r.Dids).ToHashSet();
                return otherDids.Contains(d.Did) || otherDids.Any(x => x.StartsWith(d.Did + "."));
            }) && !currentHomeDevices.Contains(d)).ToList();

            currentHomeDevices.AddRange(unassigned);
        }

        roomCategories[0].DeviceCount = currentHomeDevices.Count;

        Rooms.Clear();
        foreach (var r in roomCategories)
        {
            Rooms.Add(r);
        }
        SelectedRoom = Rooms.FirstOrDefault();

        XiaoAiVm.RefreshSpeakers(currentHomeDevices);

        _allDeviceCards.Clear();
        foreach (var dev in currentHomeDevices)
        {
            if (didToRoomName.TryGetValue(dev.Did, out var rName))
            {
                dev.RoomName = rName;
            }
            else
            {
                dev.RoomName = "未分配";
            }

            var card = new DeviceCardViewModel(dev, _jobChannel, _coordinator);
            card.OpenDetailRequested += OnCardOpenDetailRequested;
            _allDeviceCards.Add(card);
        }

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
            await PollDevicePropertiesOnceAsync();
        });

        ApplyFilter();
        UpdateSummary();
    }

    partial void OnSearchKeywordChanged(string value) => ApplyFilter();
    partial void OnSelectedRoomChanged(RoomCategoryItem? value)
    {
        ApplyFilter();
        UpdateCurrentTabInfo();
    }

    private void ApplyFilter()
    {
        var query = _allDeviceCards.AsEnumerable();

        if (SelectedRoom != null && SelectedRoom.Id != "__ALL__")
        {
            query = query.Where(d => d.RoomName == SelectedRoom.Name);
        }

        if (!string.IsNullOrWhiteSpace(SearchKeyword))
        {
            var kw = SearchKeyword.Trim();
            query = query.Where(d =>
                d.Name.Contains(kw, StringComparison.OrdinalIgnoreCase) ||
                d.Device.Model.Contains(kw, StringComparison.OrdinalIgnoreCase) ||
                d.RoomName.Contains(kw, StringComparison.OrdinalIgnoreCase));
        }

        var list = query.ToList();

        FilteredDevices.Clear();
        foreach (var item in list)
        {
            FilteredDevices.Add(item);
        }

        // 重新构建分房间区块（供分组呈现）
        RoomSections.Clear();
        var grouped = list.GroupBy(d => d.RoomName);
        foreach (var g in grouped)
        {
            var section = new RoomSectionGroup { RoomName = g.Key };
            foreach (var card in g)
            {
                section.Devices.Add(card);
            }
            RoomSections.Add(section);
        }
    }

    private void StartBackgroundPolling()
    {
        _pollLoopCts?.Cancel();
        _pollLoopCts = new CancellationTokenSource();

        _ = Task.Run(async () =>
        {
            using var periodicTimer = new PeriodicTimer(TimeSpan.FromSeconds(5));
            while (!_pollLoopCts.Token.IsCancellationRequested &&
                   await periodicTimer.WaitForNextTickAsync(_pollLoopCts.Token))
            {
                try
                {
                    await PollDevicePropertiesOnceAsync();
                }
                catch
                {
                }
            }
        });
    }

    private async Task PollDevicePropertiesOnceAsync()
    {
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
