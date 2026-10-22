using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MiJia.Net.Cloud;
using MiJia.Net.Models.Devices;
using MiJia.Net.Models.Spec;

namespace MiHAva.UI.ViewModels.Spec;

public partial class SpecActionItemViewModel : ObservableObject
{
    private readonly IMiCloudClient _client;
    private readonly string _did;
    private readonly int _siid;
    private readonly MiotSpecAction _action;

    public string Description => _action.Description;
    public int Aiid => _action.Iid;

    [ObservableProperty]
    private bool _isExecuting;

    [ObservableProperty]
    private string? _resultMessage;

    public SpecActionItemViewModel(string did, int siid, MiotSpecAction action, IMiCloudClient client)
    {
        _did = did;
        _siid = siid;
        _action = action;
        _client = client;
    }

    [RelayCommand]
    public async Task ExecuteAsync()
    {
        if (IsExecuting) return;

        try
        {
            IsExecuting = true;
            ResultMessage = null;

            var target = new MiotActionTarget
            {
                Did = _did,
                Siid = _siid,
                Aiid = _action.Iid,
                In = [] // 默认无参 action，有参 action 可在此扩展
            };

            var res = await _client.RunActionAsync(target);
            ResultMessage = res.IsSuccess ? "执行成功" : $"执行失败 ({res.Code})";
        }
        catch (Exception ex)
        {
            ResultMessage = $"错误: {ex.Message}";
        }
        finally
        {
            IsExecuting = false;
        }
    }
}
