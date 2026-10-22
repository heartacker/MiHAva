using MiJia.Net.Devices;
using MiJia.Net.Models.Scenes;
using MiJia.Net.Models.Spec;
using MiJia.Net.Serialization;
using MiJia.Net.Spec;
using System.Text.Json;
using Xunit;

namespace MiJia.Net.Tests;

public class AlignmentFeatureTests
{
    [Fact]
    public void SubDeviceHelper_ShouldCorrectlyParseSubDevices()
    {
        string subDid = "123456789.s2";
        string normalDid = "123456789";

        Assert.True(SubDeviceHelper.IsSubDevice(subDid));
        Assert.False(SubDeviceHelper.IsSubDevice(normalDid));
        Assert.False(SubDeviceHelper.IsSubDevice(null));

        Assert.Equal("123456789", SubDeviceHelper.GetParentDid(subDid));
        Assert.Null(SubDeviceHelper.GetParentDid(normalDid));

        Assert.Equal(2, SubDeviceHelper.GetGangNumber(subDid));
        Assert.Null(SubDeviceHelper.GetGangNumber(normalDid));

        Assert.Equal("123456789.s3", SubDeviceHelper.CreateSubDid("123456789", 3));
    }

    [Fact]
    public void SpecZh_ShouldBackfillAndLookupChineseDescriptions()
    {
        // 1. 精确匹配
        Assert.Equal("音量", SpecZh.LookupZh("volume", SpecZh.PropertyZh));
        Assert.Equal("播放", SpecZh.LookupZh("play", SpecZh.ActionZh));

        // 2. 去除数字后缀模糊匹配
        Assert.Equal("开关", SpecZh.LookupZh("on-10", SpecZh.PropertyZh));
        Assert.Equal("模式", SpecZh.LookupZh("mode-4", SpecZh.PropertyZh));

        // 3. 补全描述
        string backfilled = SpecZh.PropDescZh("temperature", "Temperature");
        Assert.Equal("Temperature / 温度", backfilled);

        // 已包含中文的不重复追加
        Assert.Equal("室内温度", SpecZh.PropDescZh("temperature", "室内温度"));

        // 4. 整机 Spec 本地化
        var spec = new MiotSpecInstance
        {
            Services =
            [
                new MiotSpecService
                {
                    Iid = 1,
                    Type = "urn:miot-spec-v2:service:device-information:00007801:test:1",
                    Properties =
                    [
                        new MiotSpecProperty
                        {
                            Iid = 1,
                            Type = "urn:miot-spec-v2:property:volume:00000001:test:1",
                            Description = "Volume"
                        }
                    ],
                    Actions =
                    [
                        new MiotSpecAction
                        {
                            Iid = 1,
                            Type = "urn:miot-spec-v2:action:play:00000001:test:1",
                            Description = "Play"
                        }
                    ]
                }
            ]
        };

        SpecZh.LocalizeSpec(spec);

        Assert.Equal("Volume / 音量", spec.Services[0].Properties![0].Description);
        Assert.Equal("Play / 播放", spec.Services[0].Actions![0].Description);
        Assert.Equal("volume", spec.Services[0].Properties![0].Name);
        Assert.Equal("play", spec.Services[0].Actions![0].Name);
    }

    [Fact]
    public void MetricsFormatter_ShouldFormatTemperatureAndHumidity()
    {
        Assert.Equal("24.5°C  55%", MetricsFormatter.FormatMetricsText(24.53, 55.0));
        Assert.Equal("24°C", MetricsFormatter.FormatMetricsText(24.0, null));
        Assert.Equal("60%", MetricsFormatter.FormatMetricsText(null, 60.0));
        Assert.Null(MetricsFormatter.FormatMetricsText(null, null));
    }

    [Fact]
    public void SceneModels_Serialization_ShouldWorkUnderSourceGenerator()
    {
        var scene = new SceneInfo
        {
            SceneId = "1001",
            Name = "离家模式",
            HomeId = "home_01",
            HomeName = "温馨小家",
            Type = 2
        };

        string json = JsonSerializer.Serialize(scene, MiJiaJsonContext.Default.SceneInfo);
        Assert.Contains("\"scene_id\":\"1001\"", json);

        var restored = JsonSerializer.Deserialize(json, MiJiaJsonContext.Default.SceneInfo);
        Assert.NotNull(restored);
        Assert.Equal(scene.SceneId, restored.SceneId);
        Assert.Equal(scene.Name, restored.Name);
    }
}
