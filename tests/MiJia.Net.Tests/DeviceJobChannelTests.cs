using MiJia.Net.Cloud;
using MiJia.Net.Models.Auth;
using MiJia.Net.Models.Devices;
using MiJia.Net.State;
using Xunit;

namespace MiJia.Net.Tests;

public class DeviceJobChannelTests
{
    private sealed class MockCloudClient : IMiCloudClient
    {
        public MiAuthTokens Tokens => new();
        public readonly List<List<MiotPropSetTarget>> SentBatches = [];

        public Task<List<HomeInfo>> GetHomesAsync(CancellationToken cancellationToken = default) => Task.FromResult(new List<HomeInfo>());
        public Task<List<DeviceInfo>> GetDevicesAsync(bool getVirtualModel = true, bool getHuamiDevices = true, CancellationToken cancellationToken = default) => Task.FromResult(new List<DeviceInfo>());
        public Task<List<MiotPropGetResult>> GetPropertiesAsync(IEnumerable<MiotPropGetTarget> targets, CancellationToken cancellationToken = default) => Task.FromResult(new List<MiotPropGetResult>());

        public Task<List<MiotPropSetResult>> SetPropertiesAsync(IEnumerable<MiotPropSetTarget> targets, CancellationToken cancellationToken = default)
        {
            lock (SentBatches)
            {
                SentBatches.Add(targets.ToList());
            }
            return Task.FromResult(new List<MiotPropSetResult>());
        }

        public Task<MiotActionResult> RunActionAsync(MiotActionTarget target, CancellationToken cancellationToken = default) => Task.FromResult(new MiotActionResult());
        public Task<List<MiJia.Net.Models.Scenes.SceneInfo>> GetScenesAsync(string? homeId = null, CancellationToken cancellationToken = default) => Task.FromResult(new List<MiJia.Net.Models.Scenes.SceneInfo>());
        public Task<bool> RunSceneAsync(string sceneId, string? homeId = null, CancellationToken cancellationToken = default) => Task.FromResult(true);
    }

    [Fact]
    public async Task EnqueueSetProperty_ShouldDebounceRapidChanges()
    {
        var mockClient = new MockCloudClient();
        var coordinator = new DeviceStateCoordinator();
        await using var channel = new DeviceJobChannel(mockClient, coordinator);

        string did = "bulb_1";
        int siid = 2;
        int piid = 2; // 亮度

        // 模拟用户在 10 毫秒内连续滑动滑块
        await channel.EnqueueSetPropertyAsync(did, siid, piid, 10);
        await channel.EnqueueSetPropertyAsync(did, siid, piid, 25);
        await channel.EnqueueSetPropertyAsync(did, siid, piid, 50);
        await channel.EnqueueSetPropertyAsync(did, siid, piid, 80);

        // 等待后台 Channel 消费完毕
        await Task.Delay(300);

        lock (mockClient.SentBatches)
        {
            Assert.NotEmpty(mockClient.SentBatches);
            // 最后一批提交的值必须是用户最终确定的 80
            var allItems = mockClient.SentBatches.SelectMany(b => b).ToList();
            var lastItem = allItems.Last(x => x.Did == did && x.Siid == siid && x.Piid == piid);
            Assert.Equal(80, lastItem.Value);
        }
    }
}
