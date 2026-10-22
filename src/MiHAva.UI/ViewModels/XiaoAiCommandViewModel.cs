using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MiJia.Net.Cloud;
using MiJia.Net.Models.Devices;
using MiJia.Net.XiaoAi;

namespace MiHAva.UI.ViewModels;

public sealed class XiaoAiSpeakerItem
{
    public string Did { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public override string ToString() => Name;
}

public partial class XiaoAiCommandViewModel : ObservableObject
{
    private readonly IMiCloudClient _client;
    private readonly IXiaoAiService _xiaoAiService;

    [ObservableProperty]
    private string _commandText = string.Empty;

    [ObservableProperty]
    private XiaoAiSpeakerItem? _selectedSpeaker;

    [ObservableProperty]
    private bool _isExecuting;

    [ObservableProperty]
    private string? _feedbackMessage;

    public ObservableCollection<XiaoAiSpeakerItem> Speakers { get; } = [];

    public XiaoAiCommandViewModel(IMiCloudClient client)
    {
        _client = client;
        _xiaoAiService = new XiaoAiService(client);
    }

    /// <summary>
    /// 从当前已发现设备中刷新小爱音箱列表
    /// </summary>
    public void RefreshSpeakers(IEnumerable<DeviceInfo> devices)
    {
        Speakers.Clear();
        foreach (var dev in devices)
        {
            // 匹配小爱音箱型号或名称特征
            if (dev.Model.Contains("speaker", StringComparison.OrdinalIgnoreCase) ||
                dev.Model.Contains("wifispeaker", StringComparison.OrdinalIgnoreCase) ||
                dev.Model.Contains("x08", StringComparison.OrdinalIgnoreCase) ||
                dev.Model.Contains("lx0", StringComparison.OrdinalIgnoreCase) ||
                dev.Name.Contains("小爱", StringComparison.OrdinalIgnoreCase) ||
                dev.Name.Contains("音箱", StringComparison.OrdinalIgnoreCase))
            {
                Speakers.Add(new XiaoAiSpeakerItem
                {
                    Did = dev.Did,
                    Name = string.IsNullOrWhiteSpace(dev.Name) ? dev.Model : dev.Name
                });
            }
        }

        if (Speakers.Count > 0)
        {
            SelectedSpeaker = Speakers[0];
        }
    }

    /// <summary>
    /// 发送文字指令给小爱
    /// </summary>
    [RelayCommand]
    public async Task SendCommandAsync()
    {
        if (SelectedSpeaker == null)
        {
            FeedbackMessage = "请先选择可用的小爱音箱";
            return;
        }

        if (string.IsNullOrWhiteSpace(CommandText))
        {
            FeedbackMessage = "指令内容不能为空";
            return;
        }

        string cmd = CommandText.Trim();
        try
        {
            IsExecuting = true;
            FeedbackMessage = "正在发送指令...";

            bool success = await _xiaoAiService.ExecuteTextDirectiveAsync(
                SelectedSpeaker.Did,
                cmd,
                silent: false);

            if (success)
            {
                FeedbackMessage = $"已发送: 「{cmd}」";
                CommandText = string.Empty; // 发送成功清空输入框
            }
            else
            {
                FeedbackMessage = "发送指令失败，请检查音箱是否在线";
            }
        }
        catch (Exception ex)
        {
            FeedbackMessage = $"执行异常: {ex.Message}";
        }
        finally
        {
            IsExecuting = false;
        }
    }
}
