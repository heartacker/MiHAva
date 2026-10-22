using System.Text.Json;
using MiJia.Net.Models.Spec;
using Xunit;

namespace MiJia.Net.Tests;

public class MiotSpecTests
{
    [Fact]
    public void Deserialize_DeviceSpecJson_ShouldParseServicesAndProperties()
    {
        string sampleSpecJson = """
        {
          "type": "urn:miot-spec-v2:device:switch:0000A003:cuco-cp1:1",
          "description": "Switch",
          "services": [
            {
              "iid": 1,
              "type": "urn:miot-spec-v2:service:device-information:00007801:cuco-cp1:1",
              "description": "Device Information",
              "properties": [
                {
                  "iid": 1,
                  "type": "urn:miot-spec-v2:property:manufacturer:00000001:cuco-cp1:1",
                  "description": "Device Manufacturer",
                  "format": "string",
                  "access": ["read"]
                }
              ]
            },
            {
              "iid": 2,
              "type": "urn:miot-spec-v2:service:switch:0000780C:cuco-cp1:1",
              "description": "Switch",
              "properties": [
                {
                  "iid": 1,
                  "type": "urn:miot-spec-v2:property:on:00000006:cuco-cp1:1",
                  "description": "Switch Status",
                  "format": "bool",
                  "access": ["read", "write", "notify"]
                },
                {
                  "iid": 2,
                  "type": "urn:miot-spec-v2:property:brightness:0000000D:cuco-cp1:1",
                  "description": "Brightness",
                  "format": "uint8",
                  "access": ["read", "write", "notify"],
                  "unit": "percentage",
                  "value-range": [1, 100, 1]
                },
                {
                  "iid": 3,
                  "type": "urn:miot-spec-v2:property:mode:00000008:cuco-cp1:1",
                  "description": "Mode",
                  "format": "uint8",
                  "access": ["read", "write", "notify"],
                  "value-list": [
                    { "value": 0, "description": "Auto" },
                    { "value": 1, "description": "Silent" },
                    { "value": 2, "description": "Favorite" }
                  ]
                }
              ],
              "actions": [
                {
                  "iid": 1,
                  "type": "urn:miot-spec-v2:action:toggle:00002811:cuco-cp1:1",
                  "description": "Toggle",
                  "in": [],
                  "out": []
                }
              ]
            }
          ]
        }
        """;

        var instance = JsonSerializer.Deserialize<MiotSpecInstance>(sampleSpecJson);

        Assert.NotNull(instance);
        Assert.Equal("Switch", instance.Description);
        Assert.Equal(2, instance.Services.Count);

        // 验证 Switch 服务
        var switchService = instance.Services.FirstOrDefault(s => s.Iid == 2);
        Assert.NotNull(switchService);
        Assert.Equal(3, switchService.Properties?.Count);
        Assert.Single(switchService.Actions!);

        // 验证 Bool 属性
        var onProp = switchService.Properties![0];
        Assert.Equal("bool", onProp.Format);
        Assert.True(onProp.CanRead);
        Assert.True(onProp.CanWrite);

        // 验证 Range 范围属性 (Slider 滑块)
        var brightnessProp = switchService.Properties[1];
        Assert.Equal("uint8", brightnessProp.Format);
        Assert.NotNull(brightnessProp.ValueRange);
        Assert.Equal(1, brightnessProp.ValueRange[0]);   // min
        Assert.Equal(100, brightnessProp.ValueRange[1]); // max
        Assert.Equal(1, brightnessProp.ValueRange[2]);   // step

        // 验证 Value-List 枚举属性 (ComboBox 下拉框)
        var modeProp = switchService.Properties[2];
        Assert.NotNull(modeProp.ValueList);
        Assert.Equal(3, modeProp.ValueList.Count);
        Assert.Equal("Silent", modeProp.ValueList[1].Description);

        // 验证 Action
        Assert.NotNull(switchService.Actions);
        var toggleAction = switchService.Actions[0];
        Assert.Equal("Toggle", toggleAction.Description);
        Assert.Empty(toggleAction.In);
    }
}
