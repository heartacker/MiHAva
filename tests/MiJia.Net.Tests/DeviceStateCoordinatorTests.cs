using MiJia.Net.State;
using Xunit;

namespace MiJia.Net.Tests;

public class DeviceStateCoordinatorTests
{
    [Fact]
    public void ShouldRejectOutdatedRemoteValue_DuringLeasePeriod()
    {
        var coordinator = new DeviceStateCoordinator();
        string did = "dev_plug_123";
        int siid = 2;
        int piid = 1;

        // 模拟用户在客户端把开关设为 true
        coordinator.RecordLocalChange(did, siid, piid, targetValue: true, duration: TimeSpan.FromSeconds(2));

        // 模拟在租约期内，云端定时轮询返回了旧状态 false
        bool accepted = coordinator.ShouldAcceptRemoteValue(did, siid, piid, remoteValue: false, out var effectiveValue);

        // 必须拒绝旧状态，且有效值必须维持客户端期望的 true
        Assert.False(accepted);
        Assert.Equal(true, effectiveValue);
    }

    [Fact]
    public void ShouldAcceptRemoteValue_WhenRemoteMatchesTargetEarly()
    {
        var coordinator = new DeviceStateCoordinator();
        string did = "dev_plug_123";
        int siid = 2;
        int piid = 1;

        // 用户设定为 true
        coordinator.RecordLocalChange(did, siid, piid, targetValue: true, duration: TimeSpan.FromSeconds(5));

        // 云端此时也返回了 true（云端已同步成功）
        bool accepted = coordinator.ShouldAcceptRemoteValue(did, siid, piid, remoteValue: true, out var effectiveValue);

        // 此时应采纳并提前解除租约
        Assert.True(accepted);
        Assert.Equal(true, effectiveValue);
    }

    [Fact]
    public async Task ShouldAcceptRemoteValue_AfterLeaseExpires()
    {
        var coordinator = new DeviceStateCoordinator();
        string did = "dev_light_001";
        int siid = 2;
        int piid = 1;

        // 设置一个极短的 100 毫秒租约
        coordinator.RecordLocalChange(did, siid, piid, targetValue: true, duration: TimeSpan.FromMilliseconds(100));

        // 等待租约超时
        await Task.Delay(150);

        // 云端返回状态 false
        bool accepted = coordinator.ShouldAcceptRemoteValue(did, siid, piid, remoteValue: false, out var effectiveValue);

        // 租约已超时，应采纳云端实际状态
        Assert.True(accepted);
        Assert.Equal(false, effectiveValue);
    }
}
