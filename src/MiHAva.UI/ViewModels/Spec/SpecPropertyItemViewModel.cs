using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MiJia.Net.Models.Spec;
using MiJia.Net.State;

namespace MiHAva.UI.ViewModels.Spec;

public enum SpecPropertyKind
{
    Boolean,
    RangeSlider,
    ValueList,
    ReadOnlyText,
    StringText
}

public sealed class ValueOption
{
    public int Value { get; init; }
    public string Description { get; init; } = string.Empty;
    public override string ToString() => Description;
}

public partial class SpecPropertyItemViewModel : ObservableObject
{
    private readonly string _did;
    private readonly IDeviceJobChannel _jobChannel;
    private readonly IDeviceStateCoordinator _coordinator;
    private bool _isUpdatingFromRemote;

    public MiotSpecProperty Property { get; }
    public int Siid { get; }
    public int Piid => Property.Iid;
    public string Description => Property.Description;
    public string? Unit => Property.Unit;

    public SpecPropertyKind Kind { get; }

    // Range Slider 属性
    public double Minimum { get; } = 0;
    public double Maximum { get; } = 100;
    public double Step { get; } = 1;

    // 枚举列表选项
    public List<ValueOption> Options { get; } = [];

    [ObservableProperty]
    private bool _boolValue;

    [ObservableProperty]
    private double _doubleValue;

    [ObservableProperty]
    private ValueOption? _selectedOption;

    [ObservableProperty]
    private string _displayText = "--";

    [ObservableProperty]
    private bool _isBusy;

    public bool CanWrite => Property.CanWrite;
    public bool CanRead => Property.CanRead;

    public SpecPropertyItemViewModel(
        string did,
        int siid,
        MiotSpecProperty property,
        IDeviceJobChannel jobChannel,
        IDeviceStateCoordinator coordinator)
    {
        _did = did;
        Siid = siid;
        Property = property;
        _jobChannel = jobChannel;
        _coordinator = coordinator;

        // 判断属性的渲染类型
        if (property.Format == "bool")
        {
            Kind = property.CanWrite ? SpecPropertyKind.Boolean : SpecPropertyKind.ReadOnlyText;
        }
        else if (property.ValueList != null && property.ValueList.Count > 0)
        {
            Kind = property.CanWrite ? SpecPropertyKind.ValueList : SpecPropertyKind.ReadOnlyText;
            Options = property.ValueList
                .Select(x => new ValueOption { Value = x.Value, Description = x.Description })
                .ToList();
        }
        else if (property.ValueRange != null && property.ValueRange.Count >= 2)
        {
            Kind = property.CanWrite ? SpecPropertyKind.RangeSlider : SpecPropertyKind.ReadOnlyText;
            Minimum = property.ValueRange[0];
            Maximum = property.ValueRange[1];
            Step = property.ValueRange.Count >= 3 ? property.ValueRange[2] : 1;
        }
        else
        {
            Kind = SpecPropertyKind.ReadOnlyText;
        }
    }

    /// <summary>
    /// 云端状态返回时更新本地值
    /// </summary>
    public void UpdateRemoteValue(object? rawValue)
    {
        if (!_coordinator.ShouldAcceptRemoteValue(_did, Siid, Piid, rawValue, out var effective))
        {
            return;
        }

        if (effective == null) return;

        _isUpdatingFromRemote = true;
        try
        {
            switch (Kind)
            {
                case SpecPropertyKind.Boolean:
                    if (effective is bool b) BoolValue = b;
                    else if (effective is System.Text.Json.JsonElement jeb && (jeb.ValueKind == System.Text.Json.JsonValueKind.True || jeb.ValueKind == System.Text.Json.JsonValueKind.False))
                        BoolValue = jeb.GetBoolean();
                    DisplayText = BoolValue ? "开启" : "关闭";
                    break;

                case SpecPropertyKind.RangeSlider:
                    if (double.TryParse(effective.ToString(), out double d))
                    {
                        DoubleValue = d;
                        DisplayText = $"{d} {Unit}".Trim();
                    }
                    break;

                case SpecPropertyKind.ValueList:
                    if (int.TryParse(effective.ToString(), out int val))
                    {
                        SelectedOption = Options.FirstOrDefault(x => x.Value == val);
                        DisplayText = SelectedOption?.Description ?? val.ToString();
                    }
                    break;

                case SpecPropertyKind.ReadOnlyText:
                case SpecPropertyKind.StringText:
                    DisplayText = $"{effective} {Unit}".Trim();
                    break;
            }
        }
        finally
        {
            _isUpdatingFromRemote = false;
        }
    }

    partial void OnBoolValueChanged(bool value)
    {
        if (_isUpdatingFromRemote) return;
        _ = SetBoolValueAsync(value);
    }

    partial void OnDoubleValueChanged(double value)
    {
        if (_isUpdatingFromRemote) return;
        _ = SetDoubleValueAsync(value);
    }

    partial void OnSelectedOptionChanged(ValueOption? value)
    {
        if (_isUpdatingFromRemote || value == null) return;
        _ = SetOptionAsync(value);
    }

    /// <summary>
    /// 用户切换布尔开关
    /// </summary>
    [RelayCommand]
    public async Task SetBoolValueAsync(bool value)
    {
        if (!CanWrite) return;
        BoolValue = value;
        DisplayText = value ? "开启" : "关闭";
        await _jobChannel.EnqueueSetPropertyAsync(_did, Siid, Piid, value);
    }

    /// <summary>
    /// 用户拖动滑块完成
    /// </summary>
    public async Task SetDoubleValueAsync(double value)
    {
        if (!CanWrite) return;
        DoubleValue = value;
        DisplayText = $"{value} {Unit}".Trim();

        object targetVal = Property.Format.StartsWith("int") || Property.Format.StartsWith("uint")
            ? (int)Math.Round(value)
            : value;

        await _jobChannel.EnqueueSetPropertyAsync(_did, Siid, Piid, targetVal);
    }

    /// <summary>
    /// 用户选择下拉项
    /// </summary>
    public async Task SetOptionAsync(ValueOption? option)
    {
        if (!CanWrite || option == null) return;
        SelectedOption = option;
        DisplayText = option.Description;
        await _jobChannel.EnqueueSetPropertyAsync(_did, Siid, Piid, option.Value);
    }
}
