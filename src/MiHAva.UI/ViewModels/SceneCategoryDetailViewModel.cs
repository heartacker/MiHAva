using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace MiHAva.UI.ViewModels;

/// <summary>
/// 某个场景分类的房间明细详情 ViewModel（点击分类标题后打开，详查每个房间内的情况）
/// </summary>
public partial class SceneCategoryDetailViewModel : ObservableObject
{
    public SceneCategorySummaryGroup CategoryGroup { get; }

    public string Title => $"{CategoryGroup.Title}明细";
    public string Icon => CategoryGroup.Icon;

    [ObservableProperty]
    private string _subtitle;

    public ObservableCollection<SceneCategoryRoomGroup> RoomGroups => CategoryGroup.RoomGroups;

    public event Action? CloseRequested;

    public SceneCategoryDetailViewModel(SceneCategorySummaryGroup categoryGroup)
    {
        CategoryGroup = categoryGroup;
        _subtitle = $"全屋共 {categoryGroup.TotalDeviceCount} 件设备 · {categoryGroup.ActiveDeviceCount} 件正在运行";
    }

    [RelayCommand]
    public void Close()
    {
        CloseRequested?.Invoke();
    }

    [RelayCommand]
    public async Task TurnOnAllAsync()
    {
        await CategoryGroup.TurnOnAllAsync();
        Subtitle = $"全屋共 {CategoryGroup.TotalDeviceCount} 件设备 · {CategoryGroup.ActiveDeviceCount} 件正在运行";
    }

    [RelayCommand]
    public async Task TurnOffAllAsync()
    {
        await CategoryGroup.TurnOffAllAsync();
        Subtitle = $"全屋共 {CategoryGroup.TotalDeviceCount} 件设备 · {CategoryGroup.ActiveDeviceCount} 件正在运行";
    }
}
