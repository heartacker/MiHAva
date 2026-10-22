using System.Text.Json;
using MiJia.Net.Auth;
using MiJia.Net.Cloud;
using MiJia.Net.Models.Auth;
using MiJia.Net.Models.Devices;
using MiJia.Net.Spec;

Console.OutputEncoding = System.Text.Encoding.UTF8;
Console.WriteLine("========================================");
Console.WriteLine("    MiJia.Net CLI Diagnostic & Test     ");
Console.WriteLine("========================================");

var authService = new MiAuthService();
var specClient = new MiotSpecClient();

// 1. 尝试从本地加载已有的 auth.json
MiAuthTokens? tokens = await authService.LoadSavedTokensAsync();

if (tokens == null || !tokens.IsValid)
{
    Console.WriteLine("\n[!] 未检测到有效的本地凭证，启动二维码登录流程...");
    var qrResult = await authService.RequestLoginQrCodeAsync();
    Console.WriteLine($"\n[二维码图片直链]:\n{qrResult.QrUrl}\n");
    Console.WriteLine("请使用 米家 APP 扫描上方链接或二维码，等待确认中...");

    using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(3));
    while (!cts.IsCancellationRequested)
    {
        await Task.Delay(2000, cts.Token);
        var pollResult = await authService.PollQrLoginStatusAsync(qrResult, cts.Token);
        
        if (pollResult.State == QrLoginState.ScannedWaitingConfirm)
        {
            Console.WriteLine("[*] 已扫码，请在手机端点击【确认登录】...");
        }
        else if (pollResult.State == QrLoginState.Confirmed && pollResult.Tokens != null)
        {
            tokens = pollResult.Tokens;
            await authService.SaveTokensAsync(tokens);
            Console.WriteLine($"[✓] 登录成功！用户ID: {tokens.UserId}，凭据已自动保存。");
            break;
        }
        else if (pollResult.State == QrLoginState.Expired)
        {
            Console.WriteLine("[×] 二维码已失效，请重新运行程序。");
            return;
        }
        else if (pollResult.State == QrLoginState.Failed)
        {
            Console.WriteLine($"[×] 登录失败: {pollResult.Message}");
            return;
        }
    }
}
else
{
    Console.WriteLine($"[✓] 成功加载本地凭据！用户ID: {tokens.UserId}");
}

if (tokens == null)
{
    Console.WriteLine("[×] 无法获取凭据，退出。");
    return;
}

// 2. 初始化 MiCloudClient
var client = new MiCloudClient(tokens);

try
{
    Console.WriteLine("\n正在获取家庭与房间列表...");
    var homes = await client.GetHomesAsync();
    Console.WriteLine($"共找到 {homes.Count} 个家庭：");
    foreach (var h in homes)
    {
        Console.WriteLine($"  - 家庭: {h.Name} (ID: {h.Id}, 房间数: {h.Rooms.Count})");
        foreach (var r in h.Rooms)
        {
            Console.WriteLine($"      └ 房间: {r.Name} (ID: {r.Id}, 关联设备数: {r.Dids.Count})");
        }
    }

    Console.WriteLine("\n正在获取设备列表...");
    var devices = await client.GetDevicesAsync();
    Console.WriteLine($"共拉取到 {devices.Count} 个设备：\n");

    int displayLimit = Math.Min(devices.Count, 15);
    for (int i = 0; i < displayLimit; i++)
    {
        var d = devices[i];
        string status = d.IsOnline ? "[在线]" : "[离线]";
        Console.WriteLine($"  [{i + 1:D2}] {status} {d.Name} | 型号: {d.Model} | DID: {d.Did}");
        if (!string.IsNullOrEmpty(d.SpecType))
        {
            Console.WriteLine($"       Spec: {d.SpecType}");
        }
    }

    if (devices.Count > displayLimit)
    {
        Console.WriteLine($"  ... 以及另外 {devices.Count - displayLimit} 个设备");
    }

    // 3. 测试拉取第一个支持 Spec 的设备的定义
    var firstWithSpec = devices.FirstOrDefault(d => !string.IsNullOrEmpty(d.SpecType));
    if (firstWithSpec != null)
    {
        Console.WriteLine($"\n测试拉取设备 [{firstWithSpec.Name}] 的 MIoT Spec 规格元数据...");
        var spec = await specClient.GetSpecInstanceAsync(firstWithSpec.SpecType!);
        if (spec != null)
        {
            Console.WriteLine($"[✓] Spec 实例解析成功: {spec.Description} (包含 {spec.Services.Count} 个服务)");
            foreach (var s in spec.Services)
            {
                Console.WriteLine($"    服务: {s.Description} (siid: {s.Iid})");
                if (s.Properties != null)
                {
                    foreach (var p in s.Properties.Take(5))
                    {
                        string rw = string.Join(",", p.Access);
                        Console.WriteLine($"      - 属性: {p.Description} (piid: {p.Iid}, 格式: {p.Format}, [{rw}])");
                    }
                }
            }
        }
    }

    Console.WriteLine("\n========================================");
    Console.WriteLine("    [✓] MiJia.Net 核心通信层验证完成！    ");
    Console.WriteLine("========================================");
}
catch (Exception ex)
{
    Console.WriteLine($"\n[×] 发生异常: {ex.Message}");
    Console.WriteLine(ex.ToString());
}
