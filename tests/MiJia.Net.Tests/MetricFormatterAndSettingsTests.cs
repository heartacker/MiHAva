using System;
using MiHAva.UI.Services;
using Xunit;

namespace MiJia.Net.Tests;

public class MetricFormatterAndSettingsTests
{
    [Theory]
    [InlineData("temperature", 283.0, "28.3°C")]
    [InlineData("室内温度", 25.5, "25.5°C")]
    [InlineData("temperature", -15.0, "-15.0°C")]
    [InlineData("color-temperature", 4700.0, "4700K")]
    [InlineData("色温", 4700.0, "4700K")]
    [InlineData("relative-humidity", 683.0, "68%")]
    [InlineData("当前湿度", 55.0, "55%")]
    [InlineData("battery-level", 92.4, "92%")]
    [InlineData("electric-power", 18.56, "18.6W")]
    [InlineData("power-consumption", 1.234, "1.23kWh")]
    [InlineData("pm2.5-density", 35.0, "35 μg/m³")]
    [InlineData("illumination", 120.0, "120 lx")]
    public void MetricFormatter_ShouldFormatCorrectly(string propName, double value, string expected)
    {
        string actual = MetricFormatter.FormatMetric(propName, value);
        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("color-temperature", false)]
    [InlineData("Color Temperature", false)]
    [InlineData("色温", false)]
    [InlineData("target-temperature", false)]
    [InlineData("water-temperature", false)]
    [InlineData("temperature", true)]
    [InlineData("ambient-temperature", true)]
    [InlineData("indoor-temperature", true)]
    [InlineData("室温", true)]
    [InlineData("当前温度", true)]
    public void IsEnvironmentTemperature_ShouldDistinguishProperly(string propName, bool expected)
    {
        Assert.Equal(expected, MetricFormatter.IsEnvironmentTemperature(propName));
    }

    [Fact]
    public void DeviceSettingsService_NameOverride_ShouldWorkAndReset()
    {
        string did = "test_did_9999";
        
        // 1. 设置自定义名称
        DeviceSettingsService.SetCustomName(did, "客厅主灯 (改名)");
        Assert.Equal("客厅主灯 (改名)", DeviceSettingsService.GetCustomName(did));

        // 2. 传入 null 或空恢复默认
        DeviceSettingsService.SetCustomName(did, null);
        Assert.Null(DeviceSettingsService.GetCustomName(did));
    }

    [Fact]
    public void DeviceSettingsService_HideOfflineDevices_ToggleShouldWork()
    {
        bool initial = DeviceSettingsService.HideOfflineDevices;

        DeviceSettingsService.HideOfflineDevices = !initial;
        Assert.Equal(!initial, DeviceSettingsService.HideOfflineDevices);

        // 还原
        DeviceSettingsService.HideOfflineDevices = initial;
        Assert.Equal(initial, DeviceSettingsService.HideOfflineDevices);
    }
}
